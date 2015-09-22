using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mozu.Api;
using Autofac;
using Mozu.Api.ToolKit.Config;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace Mozu_BED_Training_Exercise_14_4
{
    [TestClass]
    public class MozuDataConnectorTests
    {
        private IApiContext _apiContext;
        private IContainer _container;

        [TestInitialize]
        public void Init()
        {
            _container = new Bootstrapper().Bootstrap().Container;
            var appSetting = _container.Resolve<IAppSetting>();
            var tenantId = int.Parse(appSetting.Settings["TenantId"].ToString());
            var siteId = int.Parse(appSetting.Settings["SiteId"].ToString());

            _apiContext = new ApiContext(tenantId, siteId);
        }

        [TestMethod]
        public void Exercise_14_1_Get_Orders()
        {
            //Create an Order resource. This resource is used to get, create, update orders
            var orderResource = new Mozu.Api.Resources.Commerce.OrderResource(_apiContext);

            //Filter orders by statuses
            var acceptedOrders = orderResource.GetOrdersAsync(filter: "Status eq 'Accepted'").Result;
            var closedOrders = orderResource.GetOrdersAsync(filter: "Status eq 'Closed'").Result;

            //Filter orders by acct number
            var orderByCustId = orderResource.GetOrdersAsync(filter: "CustomerAccountId eq '1001'").Result;

            //Filter orders by email
            var orderByEmail = orderResource.GetOrdersAsync(filter: "Email eq 'test@customer.com'").Result;

            //Filter orders by order number
            var existingOrders = orderResource.GetOrdersAsync(filter: "OrderNumber eq '1'").Result;

            //Initialize the Order variable
            Mozu.Api.Contracts.CommerceRuntime.Orders.Order existingOrder = null;
            //Check if an Order was returned
            if (existingOrders.TotalCount > 0)
            {
                //Set the Order to the first occurance in the collection
                existingOrder = existingOrders.Items[0];
            }

            if (existingOrder != null)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Order Status Values: " 
                    + Environment.NewLine +
                    "Status={0}" 
                    + Environment.NewLine + 
                    "FulfillmentStatus={1}" 
                    + Environment.NewLine + 
                    "PaymentStatus={2}" 
                    + Environment.NewLine + 
                    "ReturnStatus={3}",
                   existingOrder.Status,
                   existingOrder.FulfillmentStatus,
                   existingOrder.PaymentStatus,
                   existingOrder.ReturnStatus));


                //Write out payment statuses
                foreach (var existingPayment in existingOrder.Payments)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Payment Status Value[{0}]: Status={1}",
                        existingPayment.Id,
                        existingPayment.Status));

                    //Write out payment interaction statuses
                    foreach (var existingInteraction in existingPayment.Interactions)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("Payment Interaction Status Value[{0}]: Status={1}",
                            existingInteraction.Id,
                            existingInteraction.Status));
                    }
                }

                //Write out order package statuses
                foreach (var existingPackage in existingOrder.Packages)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Package Status Value[{0}]: Status={1}",
                        existingPackage.Id,
                        existingPackage.Status));
                }
            }
        }

        [TestMethod]
        public void Exercise_14_2_Auth_Capture_Order_Payment()
        {
            var orderNumber = 11;

            //Create an Order resource. This resource is used to get, create, update orders
            var orderResource = new Mozu.Api.Resources.Commerce.OrderResource(_apiContext);
            var paymentResource = new Mozu.Api.Resources.Commerce.Orders.PaymentResource(_apiContext);

            var existingOrder = (orderResource.GetOrdersAsync(filter: "OrderNumber eq '" + orderNumber + "'").Result).Items[0];
            Mozu.Api.Contracts.CommerceRuntime.Payments.Payment authorizedPayment = null;
            Mozu.Api.Contracts.CommerceRuntime.Payments.Payment pendingPayment = null;

            #region Add BillingInfo from Customer Object
            var customerResource = new Mozu.Api.Resources.Commerce.Customer.CustomerAccountResource(_apiContext);
            var customerAccount = customerResource.GetAccountAsync(1002).Result;

            var contactInfo = new Mozu.Api.Contracts.Core.Contact();

            foreach (var contact in customerAccount.Contacts)
            {
                foreach (var type in contact.Types)
                {
                    if (type.IsPrimary)
                    {
                        contactInfo.Address = contact.Address;
                        contactInfo.CompanyOrOrganization = contact.CompanyOrOrganization;
                        contactInfo.Email = contact.Email;
                        contactInfo.FirstName = contact.FirstName;
                        contactInfo.LastNameOrSurname = contact.LastNameOrSurname;
                        contactInfo.MiddleNameOrInitial = contact.MiddleNameOrInitial;
                        contactInfo.PhoneNumbers = contact.PhoneNumbers;
                    }
                }
            }

            var billingInfo = new Mozu.Api.Contracts.CommerceRuntime.Payments.BillingInfo()
            {
                BillingContact = contactInfo,
                IsSameBillingShippingAddress = true,
                PaymentType = "Check",
            };
            #endregion

            var action = new Mozu.Api.Contracts.CommerceRuntime.Payments.PaymentAction()
            {
                    Amount = existingOrder.Total,
                    CurrencyCode = "USD",
                    InteractionDate = DateTime.Now,
                    NewBillingInfo = billingInfo,
                    ActionName = "CreatePayment",
                    ReferenceSourcePaymentId = null,
                    CheckNumber = "1234"
            };
           
            try
            {
                authorizedPayment = existingOrder.Payments.FirstOrDefault(d => d.Status == "Authorized");
                pendingPayment = existingOrder.Payments.FirstOrDefault(d => d.Status == "Pending");
            }
            catch(Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

            if(authorizedPayment != null)
            {
                action.ActionName = "CapturePayment";
                var capturedPayment = paymentResource.PerformPaymentActionAsync(action, existingOrder.Id, authorizedPayment.Id).Result;
            }
            else if(pendingPayment != null)
            {
                action.ActionName = "CapturePayment";
                var capturedPayment = paymentResource.PerformPaymentActionAsync(action, existingOrder.Id, pendingPayment.Id).Result;
            }
            else
            {
                var authPayment = paymentResource.CreatePaymentActionAsync(action, existingOrder.Id).Result;
                
            }
        }

        [TestMethod]
        public void Exercise_14_3_Fulfill_Order_Package()
        {
            var orderResource = new Mozu.Api.Resources.Commerce.OrderResource(_apiContext);
            var packageResource = new Mozu.Api.Resources.Commerce.Orders.PackageResource(_apiContext);
            var shipmentResource = new Mozu.Api.Resources.Commerce.Orders.ShipmentResource(_apiContext);

            var fulfillmentInfoResource = new Mozu.Api.Resources.Commerce.Orders.FulfillmentInfoResource(_apiContext);

            var fulfillmentActionResource = new Mozu.Api.Resources.Commerce.Orders.FulfillmentActionResource(_apiContext);

            var orderNumber = 6;
            var filter = string.Format("OrderNumber eq '{0}'", orderNumber);

            var existingOrder = (orderResource.GetOrdersAsync(startIndex: 0, pageSize: 1, filter: filter).Result).Items[0];

            var existingOrderItems = existingOrder.Items;

            var packageItems = new List<Mozu.Api.Contracts.CommerceRuntime.Fulfillment.PackageItem>();

            foreach (var orderItem in existingOrderItems)
            {

                packageItems.Add(new Mozu.Api.Contracts.CommerceRuntime.Fulfillment.PackageItem()
                {
                    ProductCode = String.IsNullOrWhiteSpace(orderItem.Product.VariationProductCode) ? orderItem.Product.ProductCode : orderItem.Product.VariationProductCode,
                    Quantity = orderItem.Quantity,
                    FulfillmentItemType = "Physical"
                    //LineId = orderItem.LineId
                });
            }

            var package = new Mozu.Api.Contracts.CommerceRuntime.Fulfillment.Package()
            {
                Measurements = new Mozu.Api.Contracts.CommerceRuntime.Commerce.PackageMeasurements()
                {
                    Height = new Mozu.Api.Contracts.Core.Measurement()
                    {
                        Unit = "in",
                        Value = 10m
                    },
                    Length = new Mozu.Api.Contracts.Core.Measurement()
                    {
                        Unit = "in",
                        Value = 10m
                    },
                    Width = new Mozu.Api.Contracts.Core.Measurement()
                    {
                        Unit = "in",
                        Value = 10m
                    },
                    Weight = new Mozu.Api.Contracts.Core.Measurement()
                    {
                        Unit = "lbs",
                        Value = 10m
                    },
                },
                Items = packageItems,
                PackagingType = "CUSTOM",
            };

            var availableShippingMethods = shipmentResource.GetAvailableShipmentMethodsAsync(existingOrder.Id).Result;
            package.ShippingMethodCode = availableShippingMethods[0].ShippingMethodCode;
            package.ShippingMethodName = availableShippingMethods[0].ShippingMethodName;
            package.FulfillmentLocationCode = "WRH01";
            package.Code = "Package-01";
            Mozu.Api.Contracts.CommerceRuntime.Fulfillment.Package updatedPackage = null;
            try
            {
                updatedPackage = packageResource.CreatePackageAsync(package, existingOrder.Id).Result;
            }
            catch(Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

            var packageIds = new List<string>();
            if(updatedPackage == null)
            {
                packageIds.Add(existingOrder.Packages[0].Id);
            }
            else
            {
                packageIds.Add(updatedPackage.Id);
            }
            //var updatedPackageShipment = shipmentResource.CreatePackageShipmentsAsync(packageIds, existingOrder.Id).Result;

            var fulfilledShipment = fulfillmentActionResource.PerformFulfillmentActionAsync(
                new Mozu.Api.Contracts.CommerceRuntime.Fulfillment.FulfillmentAction()
                {
                    ActionName = "Ship", // {Ship,Fulfill}
                    DigitalPackageIds = new List<string>(),
                    PackageIds = packageIds,
                    PickupIds = new List<string>()
                },
                existingOrder.Id)
                .Result;
        }

        [TestMethod]
        public void Exercise_14_4_Duplicate_Order()
        {
            var filter = string.Format("OrderNumber eq '{0}'", "6");

            var orderResource = new Mozu.Api.Resources.Commerce.OrderResource(_apiContext);
            var existingOrder = (orderResource.GetOrdersAsync(startIndex: 0, pageSize: 1, filter: filter).Result).Items[0];

            existingOrder.ExternalId = existingOrder.OrderNumber.ToString();

            existingOrder.Id = Guid.NewGuid().ToString("N");
            existingOrder.OrderNumber = null;
            existingOrder.IsImport = true;

            var newOrder = orderResource.CreateOrderAsync(existingOrder).Result;

            var orderNoteResource = new Mozu.Api.Resources.Commerce.Orders.OrderNoteResource(_apiContext);

            var orderNote = new Mozu.Api.Contracts.CommerceRuntime.Orders.OrderNote()
            {
                Text = string.Format("Duplicate of original order number: {0}", existingOrder.Id)
            };

            var newOrderNote = orderNoteResource.CreateOrderNoteAsync(orderNote, newOrder.Id).Result;
        }

    }
}

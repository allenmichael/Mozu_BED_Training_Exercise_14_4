param($installPath, $toolsPath, $package, $project)

$item = $project.ProjectItems.Item("EmailTemplates").ProjectItems.Item("ErrorEmailTemplate.txt");
$item.Properties.Item("CopyToOutputDirectory").Value = 1
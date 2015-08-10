Add-Type -Path .\AutomationISE.dll
 $psISE.CurrentPowerShellTab.VerticalAddOnTools.Add(‘Azure Automation ISE Add on’, [AutomationISE.AutomationISEControl], $true)
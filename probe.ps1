$ErrorActionPreference = 'Continue'
$out = @()
function Probe($name, $sb) {
  try { $v = & $sb; $script:out += "$name = $($v | ConvertTo-Json -Compress -Depth 3)" }
  catch { $script:out += "$name = ERR: $($_.Exception.Message)" }
}
$c = Get-CimInstance -Namespace root/WMI -ClassName LENOVO_GAMEZONE_DATA

Probe 'IsSupportSmartFan' { Invoke-CimMethod -InputObject $c -MethodName IsSupportSmartFan }
Probe 'GetSmartFanMode' { Invoke-CimMethod -InputObject $c -MethodName GetSmartFanMode }
Probe 'GetSmartFanSetting' { Invoke-CimMethod -InputObject $c -MethodName GetSmartFanSetting }
Probe 'GetFanCount' { Invoke-CimMethod -InputObject $c -MethodName GetFanCount }
Probe 'GetFan1Speed' { Invoke-CimMethod -InputObject $c -MethodName GetFan1Speed }
Probe 'GetFan2Speed' { Invoke-CimMethod -InputObject $c -MethodName GetFan2Speed }
Probe 'GetFanMaxSpeed' { Invoke-CimMethod -InputObject $c -MethodName GetFanMaxSpeed }
Probe 'IsSupportFanCooling' { Invoke-CimMethod -InputObject $c -MethodName IsSupportFanCooling }
Probe 'GetFanCoolingStatus' { Invoke-CimMethod -InputObject $c -MethodName GetFanCoolingStatus }
Probe 'GetCPUTemp' { Invoke-CimMethod -InputObject $c -MethodName GetCPUTemp }
Probe 'GetGPUTemp' { Invoke-CimMethod -InputObject $c -MethodName GetGPUTemp }
Probe 'GetIRTemp' { Invoke-CimMethod -InputObject $c -MethodName GetIRTemp }
Probe 'GetTriggerTemperatureValue' { Invoke-CimMethod -InputObject $c -MethodName GetTriggerTemperatureValue }
Probe 'IsSupportCpuOC' { Invoke-CimMethod -InputObject $c -MethodName IsSupportCpuOC }
Probe 'IsSupportGpuOC' { Invoke-CimMethod -InputObject $c -MethodName IsSupportGpuOC }
Probe 'IsBIOSSupportOC' { Invoke-CimMethod -InputObject $c -MethodName IsBIOSSupportOC }
Probe 'GetGpuGpsState' { Invoke-CimMethod -InputObject $c -MethodName GetGpuGpsState }
Probe 'GetGPUPow' { Invoke-CimMethod -InputObject $c -MethodName GetGPUPow }
Probe 'GetGPUOCPow' { Invoke-CimMethod -InputObject $c -MethodName GetGPUOCPow }
Probe 'GetGPUOCType' { Invoke-CimMethod -InputObject $c -MethodName GetGPUOCType }
Probe 'IsSupportGSync' { Invoke-CimMethod -InputObject $c -MethodName IsSupportGSync }
Probe 'GetGSyncStatus' { Invoke-CimMethod -InputObject $c -MethodName GetGSyncStatus }
Probe 'IsSupportWaterCooling' { Invoke-CimMethod -InputObject $c -MethodName IsSupportWaterCooling }
Probe 'GetWaterCoolingStatus' { Invoke-CimMethod -InputObject $c -MethodName GetWaterCoolingStatus }
Probe 'IsSupportLightingFeature' { Invoke-CimMethod -InputObject $c -MethodName IsSupportLightingFeature }
Probe 'GetKeyboardLight' { Invoke-CimMethod -InputObject $c -MethodName GetKeyboardLight }
Probe 'GetKeyboardfeaturelist' { Invoke-CimMethod -InputObject $c -MethodName GetKeyboardfeaturelist }
Probe 'GetMacrokeyCount' { Invoke-CimMethod -InputObject $c -MethodName GetMacrokeyCount }
Probe 'IsSupportDisableWinKey' { Invoke-CimMethod -InputObject $c -MethodName IsSupportDisableWinKey }
Probe 'GetWinKeyStatus' { Invoke-CimMethod -InputObject $c -MethodName GetWinKeyStatus }
Probe 'IsSupportDisableTP' { Invoke-CimMethod -InputObject $c -MethodName IsSupportDisableTP }
Probe 'GetTPStatus' { Invoke-CimMethod -InputObject $c -MethodName GetTPStatus }
Probe 'GetPowerChargeMode' { Invoke-CimMethod -InputObject $c -MethodName GetPowerChargeMode }
Probe 'GetThermalTableID' { Invoke-CimMethod -InputObject $c -MethodName GetThermalTableID }
Probe 'GetVersion' { Invoke-CimMethod -InputObject $c -MethodName GetVersion }
Probe 'GetMemoryOCInfo' { Invoke-CimMethod -InputObject $c -MethodName GetMemoryOCInfo }

$out | Out-File -Encoding utf8 D:\software\fanctl\probe\probe_result.txt

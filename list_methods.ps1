$ErrorActionPreference = 'Continue'
$c = Get-CimInstance -Namespace root/WMI -ClassName LENOVO_GAMEZONE_DATA
$lines = @()
foreach ($m in $c.CimClass.CimClassMethods) {
  $in = @()
  foreach ($p in $m.Parameters) { $in += ("{0}:{1}" -f $p.Name, $p.CimType) }
  $lines += ("{0} ({1})" -f $m.Name, ($in -join ', '))
}
$lines | Out-File -Encoding utf8 D:\software\fanctl\probe\methods.txt

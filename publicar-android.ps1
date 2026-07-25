# ============================================================
#  publicar-android.ps1
#  Genera el AAB firmado listo para subir a Google Play
# ============================================================

$ProjectFile  = "AppCobros.csproj"
$OutputDir    = ".\publish-android"
$Framework    = "net10.0-android"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Libreta de Cobros — Build para Play   " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Limpiar salida anterior
if (Test-Path $OutputDir) {
	Remove-Item -Recurse -Force $OutputDir
}

# Publicar en Release (genera .aab firmado)
dotnet publish $ProjectFile `
	-f $Framework `
	-c Release `
	-p:AndroidPackageFormats=aab `
	-o $OutputDir

if ($LASTEXITCODE -eq 0) {
	Write-Host ""
	Write-Host "✅ Build exitoso!" -ForegroundColor Green
	Write-Host ""
	Write-Host "Archivo generado:" -ForegroundColor Yellow
	Get-ChildItem -Path $OutputDir -Filter "*.aab" | ForEach-Object {
		Write-Host "   $($_.FullName)" -ForegroundColor White
	}
	Write-Host ""
	Write-Host "Próximos pasos:" -ForegroundColor Cyan
	Write-Host "  1. Abrí https://play.google.com/console" -ForegroundColor White
	Write-Host "  2. Creá una nueva app > Producción (o Prueba interna)" -ForegroundColor White
	Write-Host "  3. Subí el .aab generado arriba" -ForegroundColor White
	Write-Host "  4. Completá el formulario de la ficha (descripción, capturas, etc.)" -ForegroundColor White
	Write-Host ""
} else {
	Write-Host ""
	Write-Host "❌ Error en el build. Revisá los mensajes de arriba." -ForegroundColor Red
	Write-Host ""
	exit 1
}

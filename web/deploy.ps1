# ============================================================
#  deploy.ps1 — publica la app web en GitHub Pages (rama gh-pages)
#  Correr desde la carpeta web/:  powershell -File .\deploy.ps1
#  Resultado: https://pitu1386.github.io/AppCobros/
# ============================================================
$ErrorActionPreference = "Stop"
$proj = ".\LibretaCobros"
$repo = "https://github.com/pitu1386/AppCobros.git"
$baseHref = "/AppCobros/"

# 1. Tailwind CSS (si hay Node; si no, se usa el wwwroot/css/tailwind.css versionado)
if (Get-Command npm -ErrorAction SilentlyContinue) {
    Write-Host "Compilando Tailwind CSS..." -ForegroundColor Cyan
    Push-Location $proj
    if (-not (Test-Path .\node_modules\tailwindcss)) { npm install --no-audit --no-fund }
    npm run --silent build:css
    Pop-Location
    if ($LASTEXITCODE -ne 0) { Write-Error "Fallo la compilacion de Tailwind."; exit $LASTEXITCODE }
} else {
    Write-Host "npm no encontrado: se usa wwwroot/css/tailwind.css tal cual." -ForegroundColor Yellow
}

# 2. Publicacion .NET en carpeta limpia
if (Test-Path .\publish_output) { Remove-Item .\publish_output -Recurse -Force }
Write-Host "Compilando version de produccion..." -ForegroundColor Cyan
dotnet publish $proj -c Release -o .\publish_output
if ($LASTEXITCODE -ne 0) { Write-Error "Fallo la compilacion de produccion."; exit $LASTEXITCODE }

# 3. Ajustes para GitHub Pages: base href, .nojekyll, 404.html
Write-Host "Configurando GitHub Pages..." -ForegroundColor Cyan
New-Item -ItemType File -Force -Path ".\publish_output\wwwroot\.nojekyll" | Out-Null

$wasmJs = Get-ChildItem -Path ".\publish_output\wwwroot\_framework\blazor.webassembly*.js" | Select-Object -First 1
if ($wasmJs -and ($wasmJs.Name -ne "blazor.webassembly.js")) {
    Copy-Item $wasmJs.FullName -Destination ".\publish_output\wwwroot\_framework\blazor.webassembly.js" -Force
}
$dotnetJs = Get-ChildItem -Path ".\publish_output\wwwroot\_framework\dotnet*.js" | Where-Object { $_.Name -notmatch "runtime|native" } | Select-Object -First 1
if ($dotnetJs) { Copy-Item $dotnetJs.FullName -Destination ".\publish_output\wwwroot\_framework\dotnet.js" -Force }

$indexPath = ".\publish_output\wwwroot\index.html"
$content = [System.IO.File]::ReadAllText($indexPath, [System.Text.Encoding]::UTF8)
$content = $content.Replace('<base href="/" />', "<base href=""$baseHref"" />")
if ($wasmJs) { $content = $content.Replace('_framework/blazor.webassembly.js', "_framework/$($wasmJs.Name)") }
[System.IO.File]::WriteAllText($indexPath, $content, [System.Text.Encoding]::UTF8)
Copy-Item -Path $indexPath -Destination ".\publish_output\wwwroot\404.html" -Force

# 4. Push a gh-pages desde un directorio temporal aislado
Write-Host "Desplegando en la rama gh-pages..." -ForegroundColor Cyan
$tempDeploy = Join-Path $env:TEMP "libretacobros_ghpages"
if (Test-Path $tempDeploy) { Remove-Item $tempDeploy -Recurse -Force }
Copy-Item ".\publish_output\wwwroot" -Destination $tempDeploy -Recurse -Force

Push-Location $tempDeploy
git init -b gh-pages
git add -A
git commit -m "Deploy produccion"
git remote add origin $repo
git push -f origin gh-pages
Pop-Location
Remove-Item $tempDeploy -Recurse -Force

Write-Host ""
Write-Host "Listo. En GitHub: Settings -> Pages -> Source = rama gh-pages (una sola vez)." -ForegroundColor Green
Write-Host "URL: https://pitu1386.github.io/AppCobros/" -ForegroundColor Yellow

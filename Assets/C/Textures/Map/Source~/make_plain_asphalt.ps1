# 중앙선 없는 아스팔트 텍스처 만들기 (사용자 11/05) — 차도 텍스처(CarRoad, Road009B)에서 노란 중앙선 띠만 세로로 잘라내고 양쪽을 이어 붙인다.
#   원래 그림은 좌우가 이음매 없이 이어지므로 [중앙선 뒤 ~ 오른쪽 끝] + [왼쪽 끝 ~ 중앙선 앞] 순서로 붙이면 가운데는 원래 이음매라 깨끗하다.
#   새 그림의 오른쪽 끝 K 열은 원래 '중앙선 뒤 시작 바로 앞' 열들과 섞어서, 새 그림을 다시 반복해도 왼쪽 끝과 이음매 없이 이어지게 한다.
#   색 · 노멀(OpenGL) · AO 에 같은 자르기를 쓰고 2048 x 2048 로 맞춘다. 중앙선 위치는 색 그림에서 노란 열을 찾아 정한다.
# 쓰는 법: powershell -ExecutionPolicy Bypass -File make_plain_asphalt.ps1   (Assets/C/Textures/Map/Source~ 에서)
# 결과: Assets/C/Textures/Map/Asphalt/Asphalt_Color.png · Asphalt_NormalGL.png · Asphalt_AmbientOcclusion.png  (빌더 MapMat 이 이름 끝으로 찾는다)
param(
    [string]$SourceDir = (Join-Path $PSScriptRoot '..\CarRoad'),
    [string]$OutDir = (Join-Path $PSScriptRoot '..\Asphalt'),
    [int]$Margin = 96,    # 중앙선 양옆으로 더 잘라내는 폭 (px) — 섞는 폭 K 보다 커야 섞는 열에 노랑이 안 들어간다
    [int]$Blend = 64      # 새 이음매를 섞는 폭 (px)
)
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class PlainAsphalt
{
    public static byte[] Read(Bitmap b)
    {
        var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var a = new byte[b.Width * b.Height * 4];
        for (int y = 0; y < b.Height; y++) Marshal.Copy(d.Scan0 + y * d.Stride, a, y * b.Width * 4, b.Width * 4);
        b.UnlockBits(d);
        return a;
    }
    public static Bitmap Write(byte[] a, int w, int h)
    {
        var b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < h; y++) Marshal.Copy(a, y * w * 4, d.Scan0 + y * d.Stride, w * 4);
        b.UnlockBits(d);
        return b;
    }
    // 열마다 '노란 픽셀'(R·G 평균이 B 보다 25 이상 큼)이 나오는 줄의 비율 — threshold 를 넘는 첫 열 ~ 마지막 열
    public static int[] FindLine(byte[] a, int w, int h, double threshold)
    {
        var hits = new double[w];
        int rows = 0;
        for (int y = 0; y < h; y += 2, rows++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                double b = a[i], g = a[i + 1], r = a[i + 2];
                if ((r + g) * 0.5 - b > 25) hits[x] += 1;
            }
        int x0 = -1, x1 = -1;
        for (int x = 0; x < w; x++)
            if (hits[x] / rows > threshold) { if (x0 < 0) x0 = x; x1 = x; }
        return new[] { x0, x1 };
    }
    // 열 [cut0, cut1) 를 빼고 [cut1, w) + [0, cut0) 로 붙인다. 끝 K 열은 원래 cut1 바로 앞 K 열과 차츰 섞는다
    public static byte[] Cut(byte[] a, int w, int h, int cut0, int cut1, int K, out int nw)
    {
        nw = w - (cut1 - cut0);
        var o = new byte[nw * h * 4];
        int right = w - cut1;
        for (int y = 0; y < h; y++)
            for (int nx = 0; nx < nw; nx++)
            {
                int sx = nx < right ? cut1 + nx : nx - right;
                int dst = (y * nw + nx) * 4, src = (y * w + sx) * 4;
                if (nx >= nw - K)
                {
                    int k = nx - (nw - K);
                    double t = (k + 1) / (double)(K + 1);
                    int src2 = (y * w + (cut1 - K + k)) * 4;
                    for (int c = 0; c < 4; c++) o[dst + c] = (byte)Math.Round(a[src + c] * (1 - t) + a[src2 + c] * t);
                }
                else
                    for (int c = 0; c < 4; c++) o[dst + c] = a[src + c];
            }
        return o;
    }
}
'@

$names = @(@('_Color', 'Asphalt_Color.png'), @('_NormalGL', 'Asphalt_NormalGL.png'), @('_AmbientOcclusion', 'Asphalt_AmbientOcclusion.png'))
New-Item -ItemType Directory -Force $OutDir | Out-Null
$colorFile = Get-ChildItem $SourceDir -Filter '*_Color.jpg' | Select-Object -First 1
$bmp = [System.Drawing.Bitmap]::FromFile($colorFile.FullName)
$w = $bmp.Width; $h = $bmp.Height
$line = [PlainAsphalt]::FindLine([PlainAsphalt]::Read($bmp), $w, $h, 0.05)
$bmp.Dispose()
if ($line[0] -lt 0) { throw '중앙선(노란 열)을 못 찾았다' }
$cut0 = $line[0] - $Margin; $cut1 = $line[1] + 1 + $Margin
"중앙선 열 {0}~{1} → 잘라낼 열 {2}~{3} ({4} px)" -f $line[0], $line[1], $cut0, ($cut1 - 1), ($cut1 - $cut0)

foreach ($n in $names) {
    $src = Get-ChildItem $SourceDir -Filter "*$($n[0]).jpg" | Select-Object -First 1
    if (-not $src) { "건너뜀: $($n[0]) 없음"; continue }
    $b = [System.Drawing.Bitmap]::FromFile($src.FullName)
    $nw = 0
    $cut = [PlainAsphalt]::Cut([PlainAsphalt]::Read($b), $b.Width, $b.Height, $cut0, $cut1, $Blend, [ref]$nw)
    $b.Dispose()
    $narrow = [PlainAsphalt]::Write($cut, $nw, $h)
    # 2048 x 2048 로 — 반복되는 그림이라 가장자리도 반복으로 읽어 늘린다
    $out = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($out)
    $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
    $attr = New-Object System.Drawing.Imaging.ImageAttributes
    $attr.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::Tile)
    $g.DrawImage($narrow, (New-Object System.Drawing.Rectangle 0, 0, $w, $h), 0, 0, $nw, $h, [System.Drawing.GraphicsUnit]::Pixel, $attr)
    $g.Dispose(); $narrow.Dispose()
    $path = Join-Path $OutDir $n[1]
    $out.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $out.Dispose()
    "만듦: $path (가운데 {0} px 를 빼고 {1} px → {2} px 로 늘림)" -f ($cut1 - $cut0), $nw, $w
}

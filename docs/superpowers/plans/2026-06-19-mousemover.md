# MouseMover Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Windows 트레이 유틸리티 — 마우스 지글 + Win32 API로 절전 방지, 모든 모니터를 검은 전체화면으로 가리고 우하단에 상태 표기, 아무 키/클릭으로 해제.

**Architecture:** C# WinForms(.NET 8) 트레이 앱. `TrayAppContext`가 `KeepAwake`(절전방지)와 `OverlayManager`(다중 모니터 검은 폼)를 조율. P/Invoke는 `NativeMethods`에 격리. SendInput을 인터페이스(`IInputSender`)로 추상화해 핵심 로직을 단위 테스트.

**Tech Stack:** C# / .NET 8 (`net8.0-windows`), WinForms, xUnit (테스트), PowerShell + System.Drawing (아이콘 생성).

## Global Constraints

- Target framework: `net8.0-windows`, `<UseWindowsForms>true</UseWindowsForms>`.
- 단일 인스턴스: named `Mutex`로 중복 실행 차단.
- 절전방지 = 마우스 지글 + `SetThreadExecutionState` 둘 다. 실행상태 플래그는 중지/종료 시 반드시 복원.
- 지글 간격 상수 45초.
- 해제 트리거: KeyDown + MouseDown(클릭)만. 마우스 *이동*은 무시.
- 모든 모니터(`Screen.AllScreens`)를 가린다.
- 우하단 라벨 내용: `절전방지 중` / 경과시간 `HH:MM:SS` / `아무 키나 클릭하면 해제`. 1초마다 갱신.
- 빌드: `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true` → 단일 `MouseMover.exe`.
- 솔루션 루트는 repo 루트. 소스는 `src/MouseMover/`, 테스트는 `tests/MouseMover.Tests/`.

---

## File Structure

| 파일 | 책임 |
|---|---|
| `MouseMover.sln` | 솔루션 (app + tests 프로젝트 참조) |
| `src/MouseMover/MouseMover.csproj` | 앱 프로젝트 설정, ApplicationIcon |
| `src/MouseMover/NativeMethods.cs` | P/Invoke (SendInput, INPUT/MOUSEINPUT, SetThreadExecutionState, ES_*) + `IInputSender`/`Win32InputSender` |
| `src/MouseMover/TimeFormat.cs` | 경과시간 → `HH:MM:SS` 순수 함수 |
| `src/MouseMover/KeepAwake.cs` | 지글 타이머 + 실행상태 set/restore |
| `src/MouseMover/OverlayForm.cs` | 검은 전체화면 폼 + 우하단 라벨 + 입력 해제 콜백 |
| `src/MouseMover/OverlayManager.cs` | 모니터마다 OverlayForm 생성/해제 + 경과시간 타이머 |
| `src/MouseMover/TrayAppContext.cs` | 트레이 아이콘 + 메뉴, KeepAwake/OverlayManager 조율 |
| `src/MouseMover/Program.cs` | 진입점 + mutex |
| `src/MouseMover/app.ico` | 트레이/앱 아이콘 |
| `tools/make-icon.ps1` | app.ico 생성 스크립트 |
| `tests/MouseMover.Tests/MouseMover.Tests.csproj` | xUnit 테스트 프로젝트 |
| `tests/MouseMover.Tests/TimeFormatTests.cs` | TimeFormat 테스트 |
| `tests/MouseMover.Tests/KeepAwakeTests.cs` | KeepAwake 지글/복원 테스트 |

---

## Task 1: 솔루션 + 프로젝트 스캐폴딩

**Files:**
- Create: `src/MouseMover/MouseMover.csproj`
- Create: `src/MouseMover/Program.cs` (임시 최소)
- Create: `tests/MouseMover.Tests/MouseMover.Tests.csproj`
- Create: `MouseMover.sln`

**Interfaces:**
- Consumes: 없음
- Produces: 빌드 가능한 솔루션. 앱 어셈블리명 `MouseMover`, 테스트는 앱 프로젝트 참조.

- [ ] **Step 1: 앱 csproj 작성**

`src/MouseMover/MouseMover.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationIcon>app.ico</ApplicationIcon>
    <AssemblyName>MouseMover</AssemblyName>
    <RootNamespace>MouseMover</RootNamespace>
  </PropertyGroup>
</Project>
```

`ApplicationIcon`은 Task 2에서 app.ico 생성 후 유효. 이 태스크에서 임시로 줄을 넣되, app.ico가 아직 없으면 빌드 실패하므로 **Task 2 완료 전까지 `<ApplicationIcon>` 줄을 주석/제거**하고 Task 2 끝에서 추가한다. 우선 제거한 상태로 둔다:
```xml
<!-- ApplicationIcon은 Task 2에서 추가 -->
```

- [ ] **Step 2: 임시 Program.cs 작성**

`src/MouseMover/Program.cs`:
```csharp
namespace MouseMover;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Task 8에서 구현
    }
}
```

- [ ] **Step 3: 테스트 csproj 작성**

`tests/MouseMover.Tests/MouseMover.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\MouseMover\MouseMover.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: 솔루션 생성 및 프로젝트 추가**

Run:
```bash
dotnet new sln -n MouseMover
dotnet sln add src/MouseMover/MouseMover.csproj
dotnet sln add tests/MouseMover.Tests/MouseMover.Tests.csproj
```

- [ ] **Step 5: 빌드 확인**

Run: `dotnet build`
Expected: 빌드 성공(경고만 가능, 에러 0).

- [ ] **Step 6: 커밋**

```bash
git add -A
git commit -m "chore: scaffold MouseMover solution and projects"
```

---

## Task 2: 아이콘 생성

**Files:**
- Create: `tools/make-icon.ps1`
- Create: `src/MouseMover/app.ico` (스크립트 산출물)
- Modify: `src/MouseMover/MouseMover.csproj` (ApplicationIcon 추가)

**Interfaces:**
- Consumes: 없음
- Produces: `src/MouseMover/app.ico` (16/32px). Task 8의 NotifyIcon이 사용.

- [ ] **Step 1: 아이콘 생성 스크립트 작성**

`tools/make-icon.ps1` — 짙은 남색 배경 원에 흰 마우스 + 노란 초승달(zzz 절전 모티프). 32x32와 16x16을 하나의 .ico로 묶는다.
```powershell
Add-Type -AssemblyName System.Drawing

function New-Frame([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    # 배경 원 (남색)
    $bg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 30, 41, 82))
    $g.FillEllipse($bg, 1, 1, $size - 2, $size - 2)

    $s = $size / 32.0
    # 마우스 본체 (흰색 둥근 사각형 근사: 타원)
    $mouse = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $g.FillEllipse($mouse, [int](10*$s), [int](9*$s), [int](9*$s), [int](14*$s))
    # 마우스 분할선
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255,30,41,82), [single](1.2*$s))
    $g.DrawLine($pen, [single](14.5*$s), [single](9*$s), [single](14.5*$s), [single](15*$s))

    # 초승달 (노랑) — 우상단
    $moon = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 209, 102))
    $g.FillEllipse($moon, [int](19*$s), [int](4*$s), [int](9*$s), [int](9*$s))
    $cut = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 30, 41, 82))
    $g.FillEllipse($cut, [int](21.5*$s), [int](3*$s), [int](8*$s), [int](8*$s))

    $g.Dispose()
    return $bmp
}

$frames = @(16, 32) | ForEach-Object { New-Frame $_ }

# .ico 직접 쓰기 (ICONDIR + ICONDIRENTRY + PNG 데이터)
$pngs = $frames | ForEach-Object {
    $ms = New-Object System.IO.MemoryStream
    $_.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    ,$ms.ToArray()
}

$out = Join-Path $PSScriptRoot '..\src\MouseMover\app.ico'
$fs = [System.IO.File]::Create((Resolve-Path -LiteralPath (Split-Path $out)).Path + '\' + (Split-Path $out -Leaf))
$bw = New-Object System.IO.BinaryWriter($fs)
# ICONDIR
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$pngs.Count)
$offset = 6 + 16 * $pngs.Count
for ($i = 0; $i -lt $pngs.Count; $i++) {
    $sz = $frames[$i].Width
    $bw.Write([byte]($(if ($sz -ge 256) {0} else {$sz})))   # width
    $bw.Write([byte]($(if ($sz -ge 256) {0} else {$sz})))   # height
    $bw.Write([byte]0); $bw.Write([byte]0)                   # colors, reserved
    $bw.Write([uint16]1); $bw.Write([uint16]32)              # planes, bpp
    $bw.Write([uint32]$pngs[$i].Length)                      # bytes
    $bw.Write([uint32]$offset)                               # offset
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush(); $bw.Close()
Write-Host "Wrote $out"
```

- [ ] **Step 2: 스크립트 실행**

Run: `powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1`
Expected: `Wrote ...app.ico` 출력, `src/MouseMover/app.ico` 생성.

- [ ] **Step 3: 아이콘 유효성 확인**

Run: `powershell -Command "Add-Type -AssemblyName System.Drawing; $i=New-Object System.Drawing.Icon('src/MouseMover/app.ico'); Write-Host $i.Width 'x' $i.Height"`
Expected: 크기 출력(에러 없이 로드). 에러나면 Step 1 수정.

- [ ] **Step 4: csproj에 ApplicationIcon 추가**

`src/MouseMover/MouseMover.csproj`의 주석 줄을 교체:
```xml
<ApplicationIcon>app.ico</ApplicationIcon>
```

- [ ] **Step 5: 빌드 확인**

Run: `dotnet build`
Expected: 성공.

- [ ] **Step 6: 커밋**

```bash
git add -A
git commit -m "feat: add app icon and generator script"
```

---

## Task 3: 경과시간 포맷 (TimeFormat)

**Files:**
- Create: `src/MouseMover/TimeFormat.cs`
- Test: `tests/MouseMover.Tests/TimeFormatTests.cs`

**Interfaces:**
- Consumes: 없음
- Produces: `static string MouseMover.TimeFormat.Elapsed(TimeSpan span)` → `"HH:MM:SS"` (시는 자리 제한 없이 누적, 분·초는 2자리).

- [ ] **Step 1: 실패 테스트 작성**

`tests/MouseMover.Tests/TimeFormatTests.cs`:
```csharp
using MouseMover;
using Xunit;

public class TimeFormatTests
{
    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(5, "00:00:05")]
    [InlineData(65, "00:01:05")]
    [InlineData(3661, "01:01:01")]
    [InlineData(36000, "10:00:00")]
    public void Elapsed_formats_as_HHMMSS(int totalSeconds, string expected)
    {
        var result = TimeFormat.Elapsed(TimeSpan.FromSeconds(totalSeconds));
        Assert.Equal(expected, result);
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter TimeFormatTests`
Expected: FAIL (TimeFormat 타입 없음 → 컴파일 에러).

- [ ] **Step 3: 최소 구현**

`src/MouseMover/TimeFormat.cs`:
```csharp
namespace MouseMover;

public static class TimeFormat
{
    public static string Elapsed(TimeSpan span)
    {
        int totalHours = (int)span.TotalHours;
        return $"{totalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test --filter TimeFormatTests`
Expected: PASS (5건).

- [ ] **Step 5: 커밋**

```bash
git add -A
git commit -m "feat: add elapsed time formatter"
```

---

## Task 4: P/Invoke + 입력 추상화 (NativeMethods)

**Files:**
- Create: `src/MouseMover/NativeMethods.cs`

**Interfaces:**
- Consumes: 없음
- Produces:
  - `interface MouseMover.IInputSender { void Jiggle(); }`
  - `class MouseMover.Win32InputSender : IInputSender` — SendInput으로 +1/-1 상대이동.
  - `static class MouseMover.NativeMethods`:
    - `static uint SetThreadExecutionState(uint esFlags)` (P/Invoke)
    - 상수 `ES_CONTINUOUS=0x80000000`, `ES_SYSTEM_REQUIRED=0x00000001`, `ES_DISPLAY_REQUIRED=0x00000002`

- [ ] **Step 1: NativeMethods 구현 (테스트 불가한 P/Invoke 경계)**

이 태스크는 OS 호출 경계라 단위 테스트 대신 컴파일 + Task 5 통합으로 검증한다.

`src/MouseMover/NativeMethods.cs`:
```csharp
using System.Runtime.InteropServices;

namespace MouseMover;

public interface IInputSender
{
    void Jiggle();
}

public sealed class Win32InputSender : IInputSender
{
    public void Jiggle()
    {
        // 상대이동 +1px 후 -1px → 제자리, 유휴 타이머 리셋
        SendMove(1, 1);
        SendMove(-1, -1);
    }

    private static void SendMove(int dx, int dy)
    {
        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    mouseData = 0,
                    dwFlags = NativeMethods.MOUSEEVENTF_MOVE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        var inputs = new[] { input };
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}

public static class NativeMethods
{
    public const uint ES_CONTINUOUS = 0x80000000;
    public const uint ES_SYSTEM_REQUIRED = 0x00000001;
    public const uint ES_DISPLAY_REQUIRED = 0x00000002;

    public const int INPUT_MOUSE = 0;
    public const uint MOUSEEVENTF_MOVE = 0x0001;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint SetThreadExecutionState(uint esFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public int type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
```

- [ ] **Step 2: 빌드 확인**

Run: `dotnet build`
Expected: 성공.

- [ ] **Step 3: 커밋**

```bash
git add -A
git commit -m "feat: add native P/Invoke and input sender"
```

---

## Task 5: 절전방지 (KeepAwake)

**Files:**
- Create: `src/MouseMover/KeepAwake.cs`
- Test: `tests/MouseMover.Tests/KeepAwakeTests.cs`

**Interfaces:**
- Consumes: `IInputSender` (Task 4)
- Produces:
  - `class MouseMover.KeepAwake`:
    - 생성자 `KeepAwake(IInputSender sender, Action<uint> setExecutionState, int jiggleSeconds = 45)`
    - `void Start()` — 실행상태 `ES_CONTINUOUS|ES_SYSTEM_REQUIRED|ES_DISPLAY_REQUIRED` 설정, 지글 타이머 시작.
    - `void Stop()` — 타이머 중지, 실행상태 `ES_CONTINUOUS` 복원. 재진입/중복 안전.
    - `void TickForTest()` — 타이머 콜백과 동일 동작(테스트용, 한 번 지글).
    - `bool IsRunning { get; }`

설계 메모: 실제 타이머는 `System.Windows.Forms.Timer`(UI 스레드) 사용. 테스트는 타이머를 돌리지 않고 `TickForTest()`와 Start/Stop의 `setExecutionState` 호출을 검증한다. `setExecutionState`를 주입해 P/Invoke를 우회.

- [ ] **Step 1: 실패 테스트 작성**

`tests/MouseMover.Tests/KeepAwakeTests.cs`:
```csharp
using MouseMover;
using Xunit;

public class KeepAwakeTests
{
    private sealed class FakeSender : IInputSender
    {
        public int JiggleCount;
        public void Jiggle() => JiggleCount++;
    }

    [Fact]
    public void Start_sets_execution_state_with_required_flags()
    {
        var sender = new FakeSender();
        uint captured = 0;
        var ka = new KeepAwake(sender, flags => captured = flags);

        ka.Start();

        Assert.True((captured & NativeMethods.ES_CONTINUOUS) != 0);
        Assert.True((captured & NativeMethods.ES_SYSTEM_REQUIRED) != 0);
        Assert.True((captured & NativeMethods.ES_DISPLAY_REQUIRED) != 0);
        Assert.True(ka.IsRunning);
    }

    [Fact]
    public void Stop_restores_continuous_only()
    {
        var sender = new FakeSender();
        uint captured = 0;
        var ka = new KeepAwake(sender, flags => captured = flags);
        ka.Start();

        ka.Stop();

        Assert.Equal(NativeMethods.ES_CONTINUOUS, captured);
        Assert.False(ka.IsRunning);
    }

    [Fact]
    public void Tick_jiggles_once()
    {
        var sender = new FakeSender();
        var ka = new KeepAwake(sender, _ => { });
        ka.Start();

        ka.TickForTest();

        Assert.Equal(1, sender.JiggleCount);
    }

    [Fact]
    public void Stop_is_idempotent()
    {
        var sender = new FakeSender();
        var ka = new KeepAwake(sender, _ => { });
        ka.Start();
        ka.Stop();
        ka.Stop(); // 두 번째 호출 예외 없어야 함
        Assert.False(ka.IsRunning);
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test --filter KeepAwakeTests`
Expected: FAIL (KeepAwake 타입 없음).

- [ ] **Step 3: 최소 구현**

`src/MouseMover/KeepAwake.cs`:
```csharp
using System.Windows.Forms;

namespace MouseMover;

public sealed class KeepAwake : IDisposable
{
    private readonly IInputSender _sender;
    private readonly Action<uint> _setExecutionState;
    private readonly Timer _timer;

    public bool IsRunning { get; private set; }

    public KeepAwake(IInputSender sender, Action<uint> setExecutionState, int jiggleSeconds = 45)
    {
        _sender = sender;
        _setExecutionState = setExecutionState;
        _timer = new Timer { Interval = jiggleSeconds * 1000 };
        _timer.Tick += (_, _) => _sender.Jiggle();
    }

    public void Start()
    {
        if (IsRunning) return;
        _setExecutionState(
            NativeMethods.ES_CONTINUOUS |
            NativeMethods.ES_SYSTEM_REQUIRED |
            NativeMethods.ES_DISPLAY_REQUIRED);
        _timer.Start();
        IsRunning = true;
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _timer.Stop();
        _setExecutionState(NativeMethods.ES_CONTINUOUS);
        IsRunning = false;
    }

    // 테스트용: 타이머 콜백과 동일하게 한 번 지글
    public void TickForTest() => _sender.Jiggle();

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test --filter KeepAwakeTests`
Expected: PASS (4건).

- [ ] **Step 5: 커밋**

```bash
git add -A
git commit -m "feat: add KeepAwake with jiggle timer and execution state"
```

---

## Task 6: 오버레이 폼 (OverlayForm)

**Files:**
- Create: `src/MouseMover/OverlayForm.cs`

**Interfaces:**
- Consumes: `TimeFormat` (Task 3)
- Produces:
  - `class MouseMover.OverlayForm : Form`:
    - 생성자 `OverlayForm(Rectangle bounds, Action onDismiss)`
    - `void UpdateElapsed(TimeSpan elapsed)` — 우하단 라벨의 경과시간 줄 갱신.
  - 검은 배경, 테두리 없음, TopMost, 작업표시줄 미표시, 커서 숨김. KeyDown/MouseDown → `onDismiss` 1회 호출.

설계 메모: GUI 폼이라 단위 테스트 대신 Task 8 수동 검증. `onDismiss`는 중복 입력에도 1회만(가드 플래그).

- [ ] **Step 1: 구현**

`src/MouseMover/OverlayForm.cs`:
```csharp
using System.Windows.Forms;
using System.Drawing;

namespace MouseMover;

public sealed class OverlayForm : Form
{
    private readonly Action _onDismiss;
    private readonly Label _label;
    private bool _dismissed;

    public OverlayForm(Rectangle bounds, Action onDismiss)
    {
        _onDismiss = onDismiss;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        BackColor = Color.Black;
        TopMost = true;
        ShowInTaskbar = false;
        Cursor.Hide();
        KeyPreview = true;
        DoubleBuffered = true;

        _label = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(160, 160, 160),
            BackColor = Color.Black,
            Font = new Font("Segoe UI", 11f, FontStyle.Regular),
            TextAlign = ContentAlignment.BottomRight
        };
        Controls.Add(_label);
        UpdateElapsed(TimeSpan.Zero);
        Shown += (_, _) => PositionLabel();

        KeyDown += (_, _) => Dismiss();
        MouseDown += (_, _) => Dismiss();
    }

    public void UpdateElapsed(TimeSpan elapsed)
    {
        _label.Text =
            "절전방지 중\n" +
            TimeFormat.Elapsed(elapsed) + "\n" +
            "아무 키나 클릭하면 해제";
        PositionLabel();
    }

    private void PositionLabel()
    {
        // 우하단에서 24px 여백
        _label.Location = new Point(
            ClientSize.Width - _label.Width - 24,
            ClientSize.Height - _label.Height - 24);
    }

    private void Dismiss()
    {
        if (_dismissed) return;
        _dismissed = true;
        _onDismiss();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Cursor.Show();
        base.Dispose(disposing);
    }
}
```

- [ ] **Step 2: 빌드 확인**

Run: `dotnet build`
Expected: 성공.

- [ ] **Step 3: 커밋**

```bash
git add -A
git commit -m "feat: add black overlay form with status label"
```

---

## Task 7: 오버레이 매니저 (OverlayManager)

**Files:**
- Create: `src/MouseMover/OverlayManager.cs`

**Interfaces:**
- Consumes: `OverlayForm` (Task 6)
- Produces:
  - `class MouseMover.OverlayManager`:
    - 생성자 `OverlayManager(Action onDismiss)`
    - `void Start()` — `Screen.AllScreens`마다 OverlayForm 생성·표시, 경과시간 타이머(1초) 시작, 시작 시각 기록.
    - `void Stop()` — 경과시간 타이머 중지, 모든 폼 Close/Dispose. 재진입 안전.
    - `bool IsActive { get; }`
  - 임의 폼 입력 → 내부에서 `Stop()` 후 `onDismiss` 호출. 매 1초 모든 폼 `UpdateElapsed`.

설계 메모: 다중 모니터·타이머 GUI라 수동 검증. 시작 시각은 `DateTime.UtcNow`(테스트 대상 아님).

- [ ] **Step 1: 구현**

`src/MouseMover/OverlayManager.cs`:
```csharp
using System.Windows.Forms;
using System.Drawing;

namespace MouseMover;

public sealed class OverlayManager
{
    private readonly Action _onDismiss;
    private readonly List<OverlayForm> _forms = new();
    private readonly Timer _elapsedTimer;
    private DateTime _startUtc;

    public bool IsActive { get; private set; }

    public OverlayManager(Action onDismiss)
    {
        _onDismiss = onDismiss;
        _elapsedTimer = new Timer { Interval = 1000 };
        _elapsedTimer.Tick += (_, _) => RefreshElapsed();
    }

    public void Start()
    {
        if (IsActive) return;
        _startUtc = DateTime.UtcNow;

        foreach (var screen in Screen.AllScreens)
        {
            var form = new OverlayForm(screen.Bounds, DismissAll);
            _forms.Add(form);
            form.Show();
        }
        // 첫 폼에 포커스 → 키 입력 수신
        if (_forms.Count > 0) _forms[0].Activate();

        _elapsedTimer.Start();
        IsActive = true;
    }

    public void Stop()
    {
        if (!IsActive) return;
        _elapsedTimer.Stop();
        foreach (var form in _forms)
        {
            form.Close();
            form.Dispose();
        }
        _forms.Clear();
        IsActive = false;
    }

    private void RefreshElapsed()
    {
        var elapsed = DateTime.UtcNow - _startUtc;
        foreach (var form in _forms) form.UpdateElapsed(elapsed);
    }

    private void DismissAll()
    {
        if (!IsActive) return;
        Stop();
        _onDismiss();
    }
}
```

- [ ] **Step 2: 빌드 확인**

Run: `dotnet build`
Expected: 성공.

- [ ] **Step 3: 커밋**

```bash
git add -A
git commit -m "feat: add overlay manager for multi-monitor cover"
```

---

## Task 8: 트레이 앱 + 진입점 (TrayAppContext, Program)

**Files:**
- Modify: `src/MouseMover/Program.cs`
- Create: `src/MouseMover/TrayAppContext.cs`

**Interfaces:**
- Consumes: `KeepAwake` (Task 5), `OverlayManager` (Task 7), `Win32InputSender`/`NativeMethods` (Task 4), `app.ico` (Task 2)
- Produces: 실행 가능한 트레이 앱. 단일 인스턴스.

- [ ] **Step 1: TrayAppContext 구현**

`src/MouseMover/TrayAppContext.cs`:
```csharp
using System.Windows.Forms;
using System.Drawing;

namespace MouseMover;

public sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly KeepAwake _keepAwake;
    private readonly OverlayManager _overlay;
    private readonly ToolStripMenuItem _startItem;

    public TrayAppContext()
    {
        _keepAwake = new KeepAwake(
            new Win32InputSender(),
            flags => NativeMethods.SetThreadExecutionState(flags));
        _overlay = new OverlayManager(OnDismissed);

        _startItem = new ToolStripMenuItem("덮개 시작", null, (_, _) => StartCover());
        var exitItem = new ToolStripMenuItem("종료", null, (_, _) => ExitApp());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_startItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _tray = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "MouseMover — 절전방지/화면가리기",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => StartCover();
    }

    private static Icon LoadIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "app.ico");
        return File.Exists(path) ? new Icon(path) : SystemIcons.Application;
    }

    private void StartCover()
    {
        if (_overlay.IsActive) return;
        _keepAwake.Start();
        _overlay.Start();
        _startItem.Enabled = false;
    }

    private void OnDismissed()
    {
        _keepAwake.Stop();
        _startItem.Enabled = true;
    }

    private void ExitApp()
    {
        _overlay.Stop();
        _keepAwake.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        ExitThread();
    }
}
```

설계 메모: `app.ico`를 출력 폴더로 복사해야 런타임에 로드 가능. Step 2에서 csproj에 복사 설정 추가.

- [ ] **Step 2: csproj에 app.ico 출력 복사 추가**

`src/MouseMover/MouseMover.csproj`의 `</Project>` 직전에 추가:
```xml
  <ItemGroup>
    <None Update="app.ico" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 3: Program.cs 구현 (단일 인스턴스 + 실행)**

`src/MouseMover/Program.cs` 전체 교체:
```csharp
using System.Windows.Forms;

namespace MouseMover;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "MouseMover.SingleInstance.0F3A", out bool createdNew);
        if (!createdNew) return;

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayAppContext());
    }
}
```

- [ ] **Step 4: 빌드 + 전체 테스트**

Run: `dotnet build` 다음 `dotnet test`
Expected: 빌드 성공, 테스트 전건 PASS(Task 3 + Task 5 = 9건).

- [ ] **Step 5: 수동 실행 검증**

Run: `dotnet run --project src/MouseMover`
확인 항목:
1. 트레이에 커스텀 아이콘 상주.
2. 트레이 메뉴/더블클릭 "덮개 시작" → 모든 모니터 검은 화면, 우하단에 `절전방지 중`/경과시간(증가)/해제 안내.
3. 아무 키 또는 클릭 → 즉시 해제, 트레이로 복귀.
4. 다시 시작 가능. "종료"로 트레이에서 사라짐.

(자동화 불가 — 사람이 확인. 문제 시 해당 Task로 돌아가 수정.)

- [ ] **Step 6: 커밋**

```bash
git add -A
git commit -m "feat: add tray app context and single-instance entry point"
```

---

## Task 9: 빌드 산출물 + README

**Files:**
- Create: `README.md`

**Interfaces:**
- Consumes: 전체
- Produces: 단일 exe 빌드 방법 + 사용법 문서.

- [ ] **Step 1: README 작성**

`README.md`:
```markdown
# MouseMover

PC 절전 모드 방지 + 화면 가리기 Windows 트레이 유틸리티.

## 기능
- 주기적 마우스 지글 + Win32 실행상태 API로 절전(화면/시스템) 방지.
- 모든 모니터를 검은 전체화면으로 가림. 우하단에 상태/경과시간/해제 안내.
- 아무 키 또는 마우스 클릭으로 즉시 해제.
- 트레이 아이콘으로 제어(덮개 시작 / 종료), 단일 인스턴스.

## 빌드
\`\`\`
dotnet publish src/MouseMover -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
\`\`\`
산출물: \`src/MouseMover/bin/Release/net8.0-windows/win-x64/publish/MouseMover.exe\`

## 개발
- 테스트: \`dotnet test\`
- 실행: \`dotnet run --project src/MouseMover\`
- 아이콘 재생성: \`powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1\`
\`\`\`
```

- [ ] **Step 2: 게시 빌드 확인**

Run: `dotnet publish src/MouseMover -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`
Expected: 성공, `publish/MouseMover.exe` 생성.

- [ ] **Step 3: 커밋**

```bash
git add -A
git commit -m "docs: add README with build and usage"
```

---

## Self-Review 결과

**Spec coverage:**
- 절전방지(지글+API) → Task 4, 5 ✓
- 모든 모니터 검은 덮개 → Task 6, 7 ✓
- 우하단 상태/경과시간/해제안내 → Task 6 ✓
- 아무 키/클릭 해제 → Task 6 (KeyDown/MouseDown) ✓
- 트레이 제어 → Task 8 ✓
- 단일 인스턴스 → Task 8 ✓
- 커스텀 아이콘 → Task 2 ✓
- 단위 테스트(포맷/KeepAwake) → Task 3, 5 ✓
- 단일 exe 빌드 → Task 9 ✓

**Placeholder scan:** 코드/명령 모두 실제 내용. TBD 없음.

**Type consistency:** `IInputSender.Jiggle`, `KeepAwake(IInputSender, Action<uint>, int)`, `OverlayForm(Rectangle, Action)` + `UpdateElapsed(TimeSpan)`, `OverlayManager(Action)` + `Start/Stop/IsActive`, `TimeFormat.Elapsed(TimeSpan)`, `NativeMethods.ES_*`/`SetThreadExecutionState`/`SendInput` — 태스크 간 일치 확인.

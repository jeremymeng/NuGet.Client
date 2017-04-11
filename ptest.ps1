cls
$MsBuildExe = 'C:\Program Files (x86)\Microsoft Visual Studio\Preview\Enterprise\MSBuild\15.0\Bin\MSBuild.exe'
$XUnitExe = 'C:\git\NuGet.Client\packages\xunit.runner.console.2.2.0\tools\xunit.console.exe'

& $MsBuildExe 'C:\git\NuGet.Client\src\NuGet.Core\NuGet.Protocol\NuGet.Protocol.csproj' # /verbosity:minimal
& $MsBuildExe 'C:\git\NuGet.Client\test\NuGet.Core.Tests\NuGet.Protocol.Tests\NuGet.Protocol.Tests.csproj' # /verbosity:minimal

$tests = 'NuGet.Protocol.Plugins.Tests.CancellableStreamReaderTests',`
'NuGet.Protocol.Plugins.Tests.ConnectionOptionsTests',`
'NuGet.Protocol.Plugins.Tests.ConnectionTests',`
'NuGet.Protocol.Plugins.Tests.EmbeddedSignatureVerifierTests',`
'NuGet.Protocol.Plugins.Tests.FaultTests',`
'NuGet.Protocol.Plugins.Tests.HandshakeRequestTests',`
'NuGet.Protocol.Plugins.Tests.HandshakeResponseTests',`
'NuGet.Protocol.Plugins.Tests.InitializeRequestTests',`
'NuGet.Protocol.Plugins.Tests.InitializeResponseTests',`
'NuGet.Protocol.Plugins.Tests.JsonSerializationUtilitiesTests',`
'NuGet.Protocol.Plugins.Tests.MessageDispatcherTests',`
'NuGet.Protocol.Plugins.Tests.MessageEventArgsTests',`
'NuGet.Protocol.Plugins.Tests.MessageTests',`
'NuGet.Protocol.Plugins.Tests.MessageUtilitiesTests',`
'NuGet.Protocol.Plugins.Tests.PluginDiscovererTests',`
'NuGet.Protocol.Plugins.Tests.PluginFileTests',`
'NuGet.Protocol.Plugins.Tests.PluginTests',`
'NuGet.Protocol.Plugins.Tests.ProgressTests',`
'NuGet.Protocol.Plugins.Tests.ProtocolErrorEventArgsTests',`
'NuGet.Protocol.Plugins.Tests.ReceiverTests',`
'NuGet.Protocol.Plugins.Tests.RequestHandlersTests',`
'NuGet.Protocol.Plugins.Tests.RequestIdGeneratorTests',`
'NuGet.Protocol.Plugins.Tests.SenderTests',`
'NuGet.Protocol.Plugins.Tests.SymmetricHandshakeTests',`
'NuGet.Protocol.Plugins.Tests.TimeoutUtilitiesTests',`
'NuGet.Protocol.Plugins.Tests.WindowsEmbeddedSignatureVerifierTests'

$testsArgument = [System.String]::Join(',', $tests)

dotnet vstest 'C:\git\NuGet.Client\test\NuGet.Core.Tests\NuGet.Protocol.Tests\bin\Debug\netcoreapp1.0\win7-x64\NuGet.Protocol.Tests.dll' /Tests:$testsArgument /logger:trx


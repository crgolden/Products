@{
    Schema   = 1
    Repo     = 'Products'
    Workflow = '.github/workflows/main_crgolden-products.yml'
    Sonar    = @{ ProjectKey = 'crgolden_Products' }
    Probe    = @{
        Kind        = 'dotnet'
        Projects    = @('Products.Tests.Unit', 'Products.Tests.Integration')
        Environment = @{ 'Products.Tests.Integration' = @{ ASPNETCORE_ENVIRONMENT = 'Development'; MongoDatabaseName = 'crgoldenTest' } }
    }
    Steps    = @(
        @{ Name = 'Regenerate lock files and push'; Delivery = $true }
        @{
            Name   = 'Local MongoDB service'
            Run    = @('pwsh', '-NoProfile', '-File', 'scripts/gate/Assert-TestMongo.ps1')
            Inputs = @('*')
            Always = $true
        }
        @{ Name = 'Install SonarQube Cloud scanner'; Tool = 'dotnet-sonarscanner'; Inputs = @('*') }
        @{ Name = 'Restore local tools'; Run = @('dotnet', 'tool', 'restore'); Inputs = @('dotnet-tools.json'); Always = $true }
        @{
            Name   = 'Begin Sonar analysis'
            Sonar  = 'begin'
            Needs  = @('Install SonarQube Cloud scanner', 'Restore local tools')
            Inputs = @('*')
            Run    = @(
                'dotnet-sonarscanner', 'begin', '/k:{sonarProject}', '/o:crgolden', '/d:sonar.host.url=https://sonarcloud.io',
                '/d:sonar.cs.opencover.reportsPaths=coverage.opencover.xml,coverage-integration.opencover.xml',
                '/d:sonar.exclusions=**/bin/**,**/obj/**', '/d:sonar.coverage.exclusions=**/Program.cs,**/gate.ps1,**/gate.steps.psd1,scripts/gate/**',
                '/d:sonar.qualitygate.wait=true', '/d:sonar.scanner.skipJreProvisioning=true',
                '/d:sonar.scm.use.blame.algorithm=GIT_NATIVE_BLAME', '/d:sonar.branch.name={sonarBranch}'
            )
        }
        @{
            Name   = 'Build with dotnet'
            Sonar  = 'build'
            Needs  = @('Restore local tools')
            Inputs = @('*')
            Run    = @('dotnet', 'build', '--no-incremental', '--configuration', 'Release', '/p:RestoreLockedMode=true', '-warnaserror')
        }
        @{
            Name   = 'ReSharper inspection (jb inspectcode)'
            Needs  = @('Build with dotnet')
            Inputs = @('*')
            Remove = @('{output}\products-inspect.sarif')
            Run    = @('dotnet', 'jb', 'inspectcode', 'Products.slnx', '--no-build', '-e=WARNING', '--caches-home={inspectCache}', '--output={output}\products-inspect.sarif')
            Check  = @{ Sarif = '{output}\products-inspect.sarif' }
        }
        @{
            Name   = 'Run unit tests with coverage'
            Needs  = @('Build with dotnet')
            Inputs = @('*', '!Products.Tests.Integration/*')
            Remove = @('Products.Tests.Unit\bin\Release\net11.0\TestResults\unit-tests.trx')
            Run    = @(
                'dotnet', 'coverlet', 'Products.Tests.Unit/bin/Release/net11.0', '--target', 'dotnet',
                '--targetargs', 'test --project Products.Tests.Unit --no-build --configuration Release -- --filter-trait Category=Unit --report-xunit-trx --report-xunit-trx-filename unit-tests.trx --results-directory=Products.Tests.Unit/bin/Release/net11.0/TestResults',
                '--format', 'opencover', '--output', 'coverage.opencover.xml', '--skipautoprops',
                '--exclude-by-attribute', 'GeneratedCodeAttribute', '--exclude-by-file', '**/obj/**', '--exclude-by-file', '**/Program.cs',
                '--does-not-return-attribute', 'DoesNotReturnAttribute', '--include', '[Products]*'
            )
            Check  = @{ Trx = 'Products.Tests.Unit\bin\Release\net11.0\TestResults\unit-tests.trx'; Floor = 1 }
        }
        @{
            Name        = 'Run integration tests with coverage'
            Needs       = @('Build with dotnet', 'Local MongoDB service')
            Inputs      = @('*', '!Products.Tests.Unit/*Tests.cs')
            Environment = @{ ASPNETCORE_ENVIRONMENT = 'Development'; MongoDatabaseName = 'crgoldenTest' }
            Remove      = @('Products.Tests.Integration\bin\Release\net11.0\TestResults\integration-tests.trx')
            Run         = @(
                'dotnet', 'coverlet', 'Products.Tests.Integration/bin/Release/net11.0', '--target', 'dotnet',
                '--targetargs', 'test --project Products.Tests.Integration --no-build --configuration Release -- --filter-trait Category=Integration --report-xunit-trx --report-xunit-trx-filename integration-tests.trx --results-directory=Products.Tests.Integration/bin/Release/net11.0/TestResults',
                '--format', 'opencover', '--output', 'coverage-integration.opencover.xml', '--skipautoprops',
                '--exclude-by-attribute', 'GeneratedCodeAttribute', '--exclude-by-file', '**/obj/**', '--exclude-by-file', '**/Program.cs',
                '--does-not-return-attribute', 'DoesNotReturnAttribute', '--include', '[Products]*'
            )
            Check       = @{ Trx = 'Products.Tests.Integration\bin\Release\net11.0\TestResults\integration-tests.trx'; Floor = 1 }
        }
        @{ Name = 'dotnet publish'; Delivery = $true }
        @{
            Name   = 'End Sonar analysis'
            Sonar  = 'end'
            Needs  = @('Begin Sonar analysis', 'Build with dotnet')
            Inputs = @('*')
            Run    = @('dotnet-sonarscanner', 'end')
        }
        @{ Name = 'Fail on open Sonar issues'; Sonar = 'issues'; Needs = @('End Sonar analysis'); Inputs = @('*') }
        @{ Name = 'Run the app on .NET 11'; Delivery = $true }
    )
}

#!groovy

def PowerShell(psCmd) {
    bat "powershell.exe -NonInteractive -NoProfile -ExecutionPolicy Bypass -Command \"$psCmd; EXIT \$global:LastExitCode\""
}

stage("tests") {
    parallel (
        windows: {
            node('nugetci-vsts-02') {
                ws("w\\${env.BRANCH_NAME.replaceAll('/', '-')}") {
                    checkout scm
                    PowerShell(". '.\\configure.ps1' -ci -v")

                    bat "\"${tool 'MSBuild'}\" SolutionName.sln /p:Configuration=Release /p:Platform=\"Any CPU\" /p:ProductVersion=1.0.0.${env.BUILD_NUMBER}"

                    try {
                        PowerShell(". '.\\build.ps1' -s14 -v -ea Stop")
                    }
                    finally {
                        junit 'artifacts/TestResults/*.xml'
                    }
                }
            }
        },
        linux: {
            node('master') {
                try {
                    sh './build.sh'
                }
                finally {
                    junit 'artifacts/TestResults/*.xml'
                }
            }
        },
        failFast: false
    )
}
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
                    try {
                        PowerShell(". '.\\build.ps1' -s14 -ci -v -ea Stop")
                    }
                    finally {
                        archiveArtifacts artifacts: 'artifacts/nupkgs/*.nupkg', fingerprint:true
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
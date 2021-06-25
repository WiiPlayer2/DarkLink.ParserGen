pipeline {
    agent {
        docker {
            image 'mcr.microsoft.com/dotnet/sdk:5.0'
        }
    }

    environment {
        DOTNET_CLI_HOME = '/tmp/DOTNET_CLI_HOME'
    }

    stages {
        stage('Build') {
            steps {
                sh './build.ps1'
            }
        }

        stage('Publish') {
            environment {
                NUGET_API_KEY = credentials('e79baea0-4120-454e-85f6-94d47a1ebefd')
                NUGET_SOURCE = 'https://api.nuget.org/v3/index.json'
            }
            steps {
                sh './publish.ps1'
            }
        }
    }
}

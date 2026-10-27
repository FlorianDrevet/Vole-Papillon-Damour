BeforeAll {
    Import-Module (Join-Path $PSScriptRoot 'WorkerDurableStatus.psm1') -Force
}

Describe 'ConvertFrom-WorkerDurableStatusResponse' {
    It 'parses an accepted response for a running orchestration' {
        $status = ConvertFrom-WorkerDurableStatusResponse -HttpStatusCode 202 -Content '{"runtimeStatus":"Running","output":null}'

        $status.runtimeStatus | Should -Be 'Running'
    }

    It 'parses a successful response for a completed orchestration' {
        $status = ConvertFrom-WorkerDurableStatusResponse -HttpStatusCode 200 -Content '{"runtimeStatus":"Completed","output":{"filled":12}}'

        $status.runtimeStatus | Should -Be 'Completed'
        $status.output.filled | Should -Be 12
    }

    It 'rejects HTTP responses other than running or completed' {
        { ConvertFrom-WorkerDurableStatusResponse -HttpStatusCode 401 -Content '{"error":"unauthorized"}' } | Should -Throw
    }
}

Describe 'Get-WorkerDurableStatus' {
    It 'retries a transient response before returning the durable status' {
        $counter = [pscustomobject] @{ Value = 0 }

        Mock Invoke-WorkerDurableStatusRequest -ModuleName WorkerDurableStatus -MockWith {
            $counter.Value++
            if ($counter.Value -eq 1) {
                return [pscustomobject] @{ HttpStatusCode = 503; Content = '' }
            }

            return [pscustomobject] @{
                HttpStatusCode = 202
                Content        = '{"runtimeStatus":"Running","output":null}'
            }
        }
        Mock Start-Sleep -ModuleName WorkerDurableStatus {}

        $status = Get-WorkerDurableStatus `
            -Uri 'https://localhost/runtime/webhooks/durabletask/instances/example' `
            -SystemKey 'test-key' `
            -MaxAttempts 2 `
            -RetryIntervalSeconds 1

        $status.runtimeStatus | Should -Be 'Running'
        $counter.Value | Should -Be 2
    }
}

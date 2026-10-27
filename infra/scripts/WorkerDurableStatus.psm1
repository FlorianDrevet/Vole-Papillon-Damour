function ConvertFrom-WorkerDurableStatusResponse {
    param(
        [Parameter(Mandatory)]
        [int] $HttpStatusCode,
        [Parameter(Mandatory)]
        [string] $Content
    )

    if ($HttpStatusCode -notin @(200, 202)) {
        throw "Durable status endpoint returned HTTP $HttpStatusCode."
    }

    try {
        $status = ConvertFrom-Json -InputObject $Content -ErrorAction Stop
    }
    catch {
        throw 'Durable status endpoint returned invalid JSON.'
    }

    if ([string]::IsNullOrWhiteSpace([string] $status.runtimeStatus)) {
        throw 'Durable status endpoint returned no runtime status.'
    }

    return $status
}

function Invoke-WorkerDurableStatusRequest {
    param(
        [Parameter(Mandatory)]
        [Uri] $Uri,
        [Parameter(Mandatory)]
        [string] $SystemKey
    )

    $client = [System.Net.Http.HttpClient]::new()
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $Uri)
    $response = $null

    try {
        [void] $request.Headers.TryAddWithoutValidation('x-functions-key', $SystemKey)
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        return [pscustomobject] @{
            HttpStatusCode = [int] $response.StatusCode
            Content        = $content
        }
    }
    finally {
        if ($null -ne $response) {
            $response.Dispose()
        }

        $request.Dispose()
        $client.Dispose()
    }
}

function Get-WorkerDurableStatus {
    param(
        [Parameter(Mandatory)]
        [Uri] $Uri,
        [Parameter(Mandatory)]
        [string] $SystemKey,
        [ValidateRange(1, 10)]
        [int] $MaxAttempts = 6,
        [ValidateRange(1, 60)]
        [int] $RetryIntervalSeconds = 10
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            $response = Invoke-WorkerDurableStatusRequest -Uri $Uri -SystemKey $SystemKey
            if ($response.HttpStatusCode -notin @(200, 202)) {
                $httpError = [InvalidOperationException]::new(
                    "Durable status endpoint returned HTTP $($response.HttpStatusCode)."
                )
                $httpError.Data['HttpStatusCode'] = $response.HttpStatusCode
                throw $httpError
            }

            return ConvertFrom-WorkerDurableStatusResponse `
                -HttpStatusCode $response.HttpStatusCode `
                -Content $response.Content
        }
        catch {
            $statusCode = $_.Exception.Data['HttpStatusCode']
            $isRetryable = $null -eq $statusCode -or
                $statusCode -in @(404, 429) -or
                $statusCode -ge 500

            if (-not $isRetryable) {
                throw 'Could not read Durable status. Check the Worker Key Vault and telemetry.'
            }

            if ($attempt -ge $MaxAttempts) {
                throw "Could not read Durable status after $MaxAttempts attempts. Check Worker telemetry."
            }

            Start-Sleep -Seconds $RetryIntervalSeconds
        }
    }
}

Export-ModuleMember -Function ConvertFrom-WorkerDurableStatusResponse, Get-WorkerDurableStatus

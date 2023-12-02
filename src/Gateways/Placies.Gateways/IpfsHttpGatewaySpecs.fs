namespace Placies.Gateways

open System.Collections.Generic
open Microsoft.Net.Http.Headers

// https://specs.ipfs.tech/http-gateways/path-gateway/
[<RequireQualifiedAccess>]
module IpfsHttpPathGatewaySpecs =

    let requestQueryParameterNames: IReadOnlySet<string> = HashSet([
        "filename" // 2.2.1
        "download" // 2.2.2
        "format" // 2.2.3
        "dag-scope" // 2.2.4
        "entity-bytes" // 2.2.5
    ])

    type HeaderNames with
        static member ServiceWorker = "Service-Worker"
        static member XIpfsPath = "X-Ipfs-Path"
        static member XIpfsRoot = "X-Ipfs-Root"
        static member ServerTiming = "Server-Timing"

    let requestHeaderNames: IReadOnlySet<string> = HashSet([
        HeaderNames.IfNoneMatch // 2.1.1
        HeaderNames.CacheControl // 2.1.2
        HeaderNames.Accept // 2.1.3
        HeaderNames.Range // 2.1.4
        HeaderNames.ServiceWorker // 2.1.5
    ])

    let responseHeaderNames: IReadOnlySet<string> = HashSet([
        HeaderNames.ETag // 3.2.1
        HeaderNames.CacheControl // 3.2.2
        HeaderNames.LastModified // 3.2.3
        HeaderNames.ContentType // 3.2.4
        HeaderNames.ContentDisposition // 3.2.5
        HeaderNames.ContentLength // 3.2.6
        HeaderNames.ContentRange // 3.2.7
        HeaderNames.AcceptRanges // 3.2.8
        HeaderNames.Location // 3.2.9
        HeaderNames.XIpfsPath // 3.2.10
        HeaderNames.XIpfsRoot // 3.2.11
        HeaderNames.XContentTypeOptions // 3.2.12
        HeaderNames.RetryAfter // 3.2.13
        HeaderNames.ServerTiming // 3.2.14
        HeaderNames.TraceParent // 3.2.15
        HeaderNames.TraceState // 3.2.16
    ])

// https://specs.ipfs.tech/http-gateways/subdomain-gateway/
[<RequireQualifiedAccess>]
module IpfsHttpSubdomainGatewaySpecs =

    let requestQueryParameterNames = IpfsHttpPathGatewaySpecs.requestQueryParameterNames

    let requestHeaderNamesExtension: IReadOnlySet<string> = HashSet([
        HeaderNames.Host // 2.1.1
        "X-Forwarded-Proto" // 2.1.2
        "X-Forwarded-Host" // 2.1.2
    ])

    let requestHeaderNames: IReadOnlySet<string> =
        let hashSet = HashSet(IpfsHttpPathGatewaySpecs.requestHeaderNames)
        hashSet.UnionWith(requestHeaderNamesExtension)
        hashSet

    let responseHeaderNames = IpfsHttpPathGatewaySpecs.responseHeaderNames

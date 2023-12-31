namespace Placies.Gateways

open System
open System.IO
open System.Net.Http
open System.Text
open System.Threading
open FsToolkit.ErrorHandling
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Http.Extensions
open Microsoft.AspNetCore.Http
open Placies
open Placies.Utils
open Placies.Utils.Collections
open Placies.Multiformats


[<AutoOpen>]
module IpfsContentRootUriExtensions =
    [<RequireQualifiedAccess>]
    module IpfsContentRoot =

        let appendToUriPath (uriBuilder: UriBuilder) (contentRoot: IpfsContentRoot) =
            let contentRootNamespace, contentRootValue = IpfsContentRoot.toNamespaceAndValue contentRoot
            uriBuilder.Path <-
                let sb = StringBuilder(uriBuilder.Path)
                if uriBuilder.Path <> "/" then
                    sb.Append('/') |> ignore
                sb.Append(contentRootNamespace).Append('/').Append(contentRootValue).ToString()

        let appendToUriSubdomain (uriBuilder: UriBuilder) (contentRoot: IpfsContentRoot) =
            let contentRootNamespace, contentRootValue = IpfsContentRoot.toNamespaceAndValue contentRoot
            uriBuilder.Host <- StringBuilder().Append(contentRootValue).Append('.').Append(contentRootNamespace).Append('.').Append(uriBuilder.Host).ToString()



type GatewayRequest = {
    ContentRoot: IpfsContentRoot
    IsSubdomain: bool
    Method: string
    PathRemainder: PathString
    QueryParams: QueryString
    Headers: IHeaderDictionary
}

[<RequireQualifiedAccess>]
module GatewayRequest =

    let parsePathToContentRoot multibaseProvider (path: PathString) =
        // let segments = httpRequest.Path.ToString().Split('/') |> Array.toList
        let segments = ArraySegment(path.ToString().Split('/'))
        match segments with
        // | "" :: "ipfs" :: cidStr :: pathRemainder ->
        | ArraySegment.Cons ("", ArraySegment.Cons ("ipfs", ArraySegment.Cons (cidStr, pathRemainder))) ->
            Some ^ result {
                let! cid = cidStr |> Cid.tryParse multibaseProvider |> Result.mapError (fun err -> $"Invalid CID: {err}")
                let pathRemainder = String.Join('/', seq { ""; yield! pathRemainder })
                return IpfsContentRoot.Ipfs cid, pathRemainder
            }
        // | "" :: "ipns" :: ipnsName :: pathRemainder ->
        | ArraySegment.Cons ("", ArraySegment.Cons ("ipns", ArraySegment.Cons (ipnsName, pathRemainder))) ->
            Some ^ result {
                let ipnsName = ipnsName |> IpfsContentRootIpns.parseIpnsName multibaseProvider true
                let pathRemainder = String.Join('/', seq { ""; yield! pathRemainder })
                return IpfsContentRoot.Ipns ipnsName, pathRemainder
            }
        | _ ->
            None

    /// <remarks>
    /// `cid.ipfs.example.tld` => remainder="example.tld"
    /// </remarks>
    let parseHostToContentRoot multibaseProvider (host: string) =
        let hostTokens = host.Split('.') |> Array.toList
        match hostTokens with
        | cidStr :: "ipfs" :: hostRemainder ->
            Some ^ result {
                let! cid = cidStr |> Cid.tryParse multibaseProvider |> Result.mapError (fun err -> $"Invalid CID: {err}")
                let hostRemainder = String.Join('.', hostRemainder)
                return IpfsContentRoot.Ipfs cid, hostRemainder
            }
        | ipnsName :: "ipns" :: hostRemainder ->
            Some ^ result {
                let ipnsName = ipnsName |> IpfsContentRootIpns.parseIpnsName multibaseProvider false
                let hostRemainder = String.Join('.', hostRemainder)
                return IpfsContentRoot.Ipns ipnsName, hostRemainder
            }
        | _ ->
            None

    let ofHttpRequestPath (httpRequest: HttpRequest) : Result<GatewayRequest, _> option =
        let multibaseProvider = httpRequest.HttpContext.RequestServices.GetRequiredService<IMultiBaseProvider>()
        match httpRequest.Path |> parsePathToContentRoot multibaseProvider with
        | Some res ->
            Some ^ result {
                let! contentRoot, pathRemainder = res |> Result.mapError (fun err -> Results.BadRequest(err))
                let queryParams =
                    let queryBuilder = QueryBuilder()
                    for queryParamName in IpfsHttpPathGatewaySpecs.requestQueryParameterNames do
                        match httpRequest.Query.TryGetValue(queryParamName) |> Option.ofTryByref with
                        | None -> ()
                        | Some queryParamValues ->
                            queryBuilder.Add(queryParamName, queryParamValues)
                    queryBuilder.ToQueryString()
                let requestHeaders =
                    let headers = HeaderDictionary()
                    for headerName in IpfsHttpPathGatewaySpecs.requestHeaderNames do
                        match httpRequest.Headers.TryGetValue(headerName) |> Option.ofTryByref with
                        | None -> ()
                        | Some headerValues ->
                            headers.Add(headerName, headerValues)
                    headers
                return {
                    ContentRoot = contentRoot
                    IsSubdomain = false
                    Method = httpRequest.Method
                    PathRemainder = PathString(pathRemainder)
                    QueryParams = queryParams
                    Headers = requestHeaders
                }
            }
        | None ->
            None

    let ofHttpRequestSubdomain (httpRequest: HttpRequest) : Result<GatewayRequest, _> option =
        let multibaseProvider = httpRequest.HttpContext.RequestServices.GetRequiredService<IMultiBaseProvider>()
        let requestHost = httpRequest.Host.Host
        if Uri.CheckHostName(requestHost) = UriHostNameType.Dns then
            match requestHost |> parseHostToContentRoot multibaseProvider with
            | Some res ->
                Some ^ result {
                    let! contentRoot, _hostRemainder = res |> Result.mapError (fun err -> Results.BadRequest(err))
                    let queryParams =
                        let queryBuilder = QueryBuilder()
                        for queryParamName in IpfsHttpSubdomainGatewaySpecs.requestQueryParameterNames do
                            match httpRequest.Query.TryGetValue(queryParamName) |> Option.ofTryByref with
                            | None -> ()
                            | Some queryParamValues ->
                                queryBuilder.Add(queryParamName, queryParamValues)
                        queryBuilder.ToQueryString()
                    let requestHeaders =
                        let headers = HeaderDictionary()
                        for headerName in IpfsHttpSubdomainGatewaySpecs.requestHeaderNames do
                            match httpRequest.Headers.TryGetValue(headerName) |> Option.ofTryByref with
                            | None -> ()
                            | Some headerValues ->
                                headers.Add(headerName, headerValues)
                        headers
                    return {
                        ContentRoot = contentRoot
                        IsSubdomain = true
                        Method = httpRequest.Method
                        PathRemainder = httpRequest.Path
                        QueryParams = queryParams
                        Headers = requestHeaders
                    }
                }
            | None ->
                None
        else
            None

    let ofHttpRequest (httpRequest: HttpRequest) =
        ofHttpRequestSubdomain httpRequest
        |> Option.orElseWith ^fun () ->
            ofHttpRequestPath httpRequest


type GatewayResponse = {
    StatusCode: int
    ResponseHeaders: IHeaderDictionary
    ResponseContentStream: Stream
}

[<RequireQualifiedAccess>]
module GatewayResponse =

    let toHttpResponse (httpResponse: HttpResponse) (gatewayResponse: GatewayResponse) (ct: CancellationToken) = task {
        httpResponse.StatusCode <- gatewayResponse.StatusCode
        for header in gatewayResponse.ResponseHeaders do
            httpResponse.Headers.Add(header)
        do! httpResponse.StartAsync(ct)
        do! gatewayResponse.ResponseContentStream.CopyToAsync(httpResponse.Body, ct)
    }


[<RequireQualifiedAccess>]
module Gateway =

    let send (httpClient: HttpClient) (gatewayRequest: GatewayRequest) (ct: CancellationToken) = task {
        let uriBuilder = UriBuilder(httpClient.BaseAddress)

        if gatewayRequest.IsSubdomain then
            IpfsContentRoot.appendToUriSubdomain uriBuilder gatewayRequest.ContentRoot
        else
            IpfsContentRoot.appendToUriPath uriBuilder gatewayRequest.ContentRoot

        uriBuilder.Path <- PathString(uriBuilder.Path).Add(gatewayRequest.PathRemainder).ToString()
        uriBuilder.Query <- gatewayRequest.QueryParams.ToString()

        let httpMethod = HttpMethod.Parse(gatewayRequest.Method)
        use requestMessage = new HttpRequestMessage(httpMethod, uriBuilder.Uri)
        for KeyValue (n, v) in gatewayRequest.Headers do
            requestMessage.Headers.Add(n, v)
        let! responseMessage = httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, ct)

        let responseHeaders =
            let headers = HeaderDictionary()
            let headerNames =
                if gatewayRequest.IsSubdomain then
                    IpfsHttpSubdomainGatewaySpecs.responseHeaderNames
                else
                    IpfsHttpPathGatewaySpecs.responseHeaderNames
            for headerName in headerNames do
                match responseMessage.Headers.TryGetValues(headerName) |> Option.ofTryByref with
                | Some headerValues ->
                    headers.Add(headerName, StringValues(headerValues |> Seq.toArray))
                | None ->
                    match responseMessage.Content.Headers.TryGetValues(headerName) |> Option.ofTryByref with
                    | Some headerValues ->
                        headers.Add(headerName, StringValues(headerValues |> Seq.toArray))
                    | None -> ()
            headers

        let! responseContentStream = task {
            match httpMethod with
            | Equals HttpMethod.Get ->
                return! responseMessage.Content.ReadAsStreamAsync(ct)
            | Equals HttpMethod.Head ->
                return Stream.Null
            | _ ->
                return raise (NotSupportedException($"Not supported http method: {httpMethod}"))
        }

        return {
            StatusCode = int responseMessage.StatusCode
            ResponseHeaders = responseHeaders
            ResponseContentStream = responseContentStream
        }
    }


// [<AutoOpen>]
// module ApplicationBuilderIpfsHttpGatewayExtensions =
//     type IApplicationBuilder with
//         member this.UseIpfsGateway(): IApplicationBuilder =
//             this.Use(Func<HttpContext, RequestDelegate, Task>(fun ctx next -> task {
//                 match ctx.Request.Method with
//                 | Equals HttpMethods.Get ->
//                     let gatewayRequest = GatewayRequest.ofHttpRequest ctx.Request
//                     match gatewayRequest with
//                     | None ->
//                         return! next.Invoke(ctx)
//                     | Some gatewayRequest ->
//                         match gatewayRequest with
//                         | Error errorResult ->
//                             do! errorResult.ExecuteAsync(ctx)
//                             return! ctx.Response.CompleteAsync()
//                         | Ok gatewayRequest ->
//                             let httpClient = ctx.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("IpfsHttpGateway")
//                             let! gatewayResponse = Gateway.send httpClient gatewayRequest
//                             do! GatewayResponse.toHttpResponse ctx.Response gatewayResponse
//                             do! ctx.Response.CompleteAsync()
//                 | _ ->
//                     return! next.Invoke(ctx)
//             }))

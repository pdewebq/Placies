namespace Placies.Gateway

open System
open System.IO
open System.Net.Http
open System.Text
open FsToolkit.ErrorHandling
open Microsoft.Extensions.Primitives
open Microsoft.AspNetCore.Http.Extensions
open Microsoft.AspNetCore.Http
open Ipfs
open Placies


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
    PathRemainder: PathString
    QueryParams: QueryString
    Headers: IHeaderDictionary
}

[<RequireQualifiedAccess>]
module GatewayRequest =

    let parsePathToContentRoot (path: PathString) =
        // let segments = httpRequest.Path.ToString().Split('/') |> Array.toList
        let segments = ArraySegment(path.ToString().Split('/'))
        match segments with
        // | "" :: "ipfs" :: cidStr :: pathRemainder ->
        | ArraySegment.Cons ("", ArraySegment.Cons ("ipfs", ArraySegment.Cons (cidStr, pathRemainder))) ->
            Some ^ result {
                let! cid = Result.tryWith (fun () -> Cid.Decode(cidStr)) |> Result.mapError (fun _ex -> "Invalid CID")
                let pathRemainder = String.Join('/', seq { ""; yield! pathRemainder })
                return IpfsContentRoot.Ipfs cid, pathRemainder
            }
        // | "" :: "ipns" :: ipnsName :: pathRemainder ->
        | ArraySegment.Cons ("", ArraySegment.Cons ("ipns", ArraySegment.Cons (ipnsName, pathRemainder))) ->
            Some ^ result {
                let ipnsName = IpfsContentRootIpns.parseIpnsName ipnsName true
                let pathRemainder = String.Join('/', seq { ""; yield! pathRemainder })
                return IpfsContentRoot.Ipns ipnsName, pathRemainder
            }
        | _ ->
            None

    /// <remarks>
    /// `cid.ipfs.example.tld` => remainder="example.tld"
    /// </remarks>
    let parseHostToContentRoot (host: string) =
        let hostTokens = host.Split('.') |> Array.toList
        match hostTokens with
        | cidStr :: "ipfs" :: hostRemainder ->
            Some ^ result {
                let! cid = Result.tryWith (fun () -> Cid.Decode(cidStr)) |> Result.mapError (fun _ex -> "Invalid CID")
                let hostRemainder = String.Join('.', hostRemainder)
                return IpfsContentRoot.Ipfs cid, hostRemainder
            }
        | ipnsName :: "ipns" :: hostRemainder ->
            Some ^ result {
                let ipnsName = IpfsContentRootIpns.parseIpnsName ipnsName false
                let hostRemainder = String.Join('.', hostRemainder)
                return IpfsContentRoot.Ipns ipnsName, hostRemainder
            }
        | _ ->
            None

    let ofHttpRequestPath (httpRequest: HttpRequest) : Result<GatewayRequest, _> option =
        match parsePathToContentRoot httpRequest.Path with
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
                    PathRemainder = PathString(pathRemainder)
                    QueryParams = queryParams
                    Headers = requestHeaders
                }
            }
        | None ->
            None

    let ofHttpRequestSubdomain (httpRequest: HttpRequest) : Result<GatewayRequest, _> option =
        let requestHost = httpRequest.Host.Host
        if Uri.CheckHostName(requestHost) = UriHostNameType.Dns then
            match parseHostToContentRoot requestHost with
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

    let toHttpResponse (httpResponse: HttpResponse) (gatewayResponse: GatewayResponse) = task {
        httpResponse.StatusCode <- gatewayResponse.StatusCode
        for header in gatewayResponse.ResponseHeaders do
            httpResponse.Headers.Add(header)
        do! gatewayResponse.ResponseContentStream.CopyToAsync(httpResponse.Body)
    }


[<RequireQualifiedAccess>]
module Gateway =

    let send (httpClient: HttpClient) (gatewayRequest: GatewayRequest) = task {
        let uriBuilder = UriBuilder(httpClient.BaseAddress)

        if gatewayRequest.IsSubdomain then
            IpfsContentRoot.appendToUriSubdomain uriBuilder gatewayRequest.ContentRoot
        else
            IpfsContentRoot.appendToUriPath uriBuilder gatewayRequest.ContentRoot

        uriBuilder.Path <- PathString(uriBuilder.Path).Add(gatewayRequest.PathRemainder).ToString()
        uriBuilder.Query <- gatewayRequest.QueryParams.ToString()

        use requestMessage = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri)
        let! responseMessage = httpClient.SendAsync(requestMessage)

        let responseHeaders =
            let headers = HeaderDictionary()
            let headerNames =
                if gatewayRequest.IsSubdomain then
                    IpfsHttpSubdomainGatewaySpecs.responseHeaderNames
                else
                    IpfsHttpPathGatewaySpecs.responseHeaderNames
            for headerName in headerNames do
                match responseMessage.Headers.TryGetValues(headerName) |> Option.ofTryByref with
                | None -> ()
                | Some headerValues ->
                    headers.Add(headerName, StringValues(headerValues |> Seq.toArray))
            headers

        let! responseContentStream = responseMessage.Content.ReadAsStreamAsync()

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

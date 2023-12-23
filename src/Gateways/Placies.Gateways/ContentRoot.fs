namespace Placies.Gateways

open FsToolkit.ErrorHandling
open Placies
open Placies.Multiformats

[<RequireQualifiedAccess>]
type IpfsContentRootIpns =
    | Key of libp2pKey: Cid
    | DnsName of dnsName: string

[<RequireQualifiedAccess>]
module IpfsContentRootIpns =

    let unescapeIpnsDnsName (dnsName: string) : string =
        // TODO: Do it without a crutchy intermediate symbol
        dnsName.Replace("--", "$").Replace('-', '.').Replace('$', '-')

    let parseLibp2pKey multibaseProvider (input: string) = result {
        let! cidOfLibp2pKey = input |> Cid.tryParse multibaseProvider |> Result.mapError (fun err -> $"Not CID: {err}")
        do! (cidOfLibp2pKey.ContentTypeCode = MultiCodecInfos.Libp2pKey.Code) |> Result.requireTrue "Not libp2p-key"
        return cidOfLibp2pKey
    }

    let parseIpnsName multibaseProvider (shouldUnescapeDnsName: bool) (ipnsName: string) : IpfsContentRootIpns =
        let cidOfLibp2pKey = ipnsName |> parseLibp2pKey multibaseProvider
        match cidOfLibp2pKey with
        | Ok cidOfLibp2pKey ->
            IpfsContentRootIpns.Key cidOfLibp2pKey
        | Error _err ->
            let dnsName = if shouldUnescapeDnsName then unescapeIpnsDnsName ipnsName else ipnsName
            IpfsContentRootIpns.DnsName dnsName

[<RequireQualifiedAccess>]
type IpfsContentRootNamespace =
    | Ipfs
    | Ipns

[<RequireQualifiedAccess>]
type IpfsContentRoot =
    | Ipfs of cid: Cid
    | Ipns of IpfsContentRootIpns

[<RequireQualifiedAccess>]
module IpfsContentRoot =

    let toNamespaceAndValue (contentRoot: IpfsContentRoot) : string * string =
        match contentRoot with
        | IpfsContentRoot.Ipfs cid ->
            "ipfs", cid.ToString()
        | IpfsContentRoot.Ipns ipnsAddress ->
            let ipnsValue =
                match ipnsAddress with
                | IpfsContentRootIpns.Key libp2PKey -> libp2PKey.ToString()
                | IpfsContentRootIpns.DnsName dnsName -> dnsName
            "ipns", ipnsValue

[<RequireQualifiedAccess>]
module IpfsContentRootNamespace =

    let parse (input: string) : IpfsContentRootNamespace option =
        match input.ToLower() with
        | "ipfs" -> Some IpfsContentRootNamespace.Ipfs
        | "ipns" -> Some IpfsContentRootNamespace.Ipns
        | _ -> None

namespace Placies.Gateways.AllowlistProxyGateway.Server

open System.IO
open System.Threading.Tasks
open Placies.Gateways

type IContentRootAllowlistProvider =
    abstract IsAllowed: contentRoot: IpfsContentRoot -> Task<bool>

type FileContentRootAllowlistProvider(filePath: string) =
    interface IContentRootAllowlistProvider with
        member this.IsAllowed(contentRoot) = task {
            let! lines = File.ReadAllLinesAsync(filePath)
            let ns, vl = contentRoot |> IpfsContentRoot.toNamespaceAndValue
            return lines |> Array.contains $"/{ns}/{vl}"
        }

namespace Placies.Gateways.AllowlistProxyGateway.Server

open System
open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open FSharp.Control.Reactive
open Microsoft.Extensions.Logging
open Placies
open Placies.Utils
open Placies.Multiformats
open Placies.Gateways

type IContentRootAllowlistProvider =
    abstract IsAllowed: contentRoot: IpfsContentRoot -> Task<bool>

type FileContentRootAllowlistProvider(logger: ILogger<FileContentRootAllowlistProvider>, multiBaseProvider: IMultiBaseProvider, filePath: string) =

    let computeAllowedContentRoots (filePath: string) : Task<HashSet<IpfsContentRoot>> = task {
        let! lines = File.ReadAllLinesAsync(filePath)
        let allowedContentRoots =
            lines
            |> Seq.map _.Trim()
            |> Seq.filter (fun line -> not (line.StartsWith('#')))
            |> Seq.map ^fun line ->
                let tokens = line.Split('/', 2, StringSplitOptions.RemoveEmptyEntries)
                let ns, vl = tokens.[0], tokens.[1]
                match ns |> IpfsContentRootNamespace.parse with
                | None ->
                    invalidOp $"Invalid content root: '%s{line}'"
                | Some IpfsContentRootNamespace.Ipfs ->
                    IpfsContentRoot.Ipfs (Cid.parse multiBaseProvider vl)
                | Some IpfsContentRootNamespace.Ipns ->
                    IpfsContentRoot.Ipns (IpfsContentRootIpns.parseIpnsName multiBaseProvider false vl)
        return HashSet(allowedContentRoots)
    }

    let mutable allowedContentRoots: IReadOnlySet<IpfsContentRoot> = Unchecked.defaultof<_>
    let fileWatcher = new FileSystemWatcher(
        Path.GetDirectoryName(filePath),
        Filter = Path.GetFileName(filePath),
        EnableRaisingEvents = true,
        NotifyFilter = NotifyFilters.LastWrite
    )

    member this.Init() = task {
        // FIXME: Raises twice for some reason
        fileWatcher.Changed
        |> Observable.flatmapTask ^fun _ev -> task {
            logger.LogInformation("File {FilePath} updated", filePath)
            let! contentRoots = computeAllowedContentRoots filePath
            return contentRoots
        }
        |> Observable.add ^fun contentRoots ->
            allowedContentRoots <- contentRoots

        let! contentRoots = computeAllowedContentRoots filePath
        allowedContentRoots <- contentRoots
    }

    interface IContentRootAllowlistProvider with
        member this.IsAllowed(contentRoot) = task {
            return allowedContentRoots.Contains(contentRoot)
        }

    interface IDisposable with
        member this.Dispose() =
            fileWatcher.Dispose()

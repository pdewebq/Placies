namespace Placies.Ipld.DagPb

open System
open System.Text
open FsToolkit.ErrorHandling
open Placies
open Placies.Utils

// Based on https://github.com/ipld/js-dag-pb/blob/27ed1722e3d1788e40f03186a1dc9b1ba69fd1d2/src/pb-decode.js

[<RequireQualifiedAccess>]
module DagPbDecode =

    let decodeBytes (buffer: ReadOnlyMemory<byte>) : Result<struct(ReadOnlyMemory<byte> * ReadOnlyMemory<byte>), exn> = result {
        let! buffer, byteLen = VarInt.parseFromMemoryAsInt32 buffer
        if byteLen > buffer.Length then
            return! Error (exn "Unexpected end of data")
        let bytes = buffer.Slice(0, byteLen)
        let buffer = buffer.Slice(byteLen)
        return struct(buffer, bytes)
    }

    let decodeKey (buffer: ReadOnlyMemory<byte>) : Result<struct(ReadOnlyMemory<byte> * int * int), exn> = result {
        let! buffer, tag = VarInt.parseFromMemoryAsInt32 buffer
        let wireType = tag &&& 0b111
        let fieldNumber = tag >>> 3
        return struct(buffer, wireType, fieldNumber)
    }

    let decodeLink (buffer: ReadOnlyMemory<byte>) : Result<PBLink, exn> =
        let rec loop
                (buffer: ReadOnlyMemory<byte>)
                (hash: Cid voption) (name: string voption) (tsize: uint64 voption)
                =
            result {
                if buffer.IsEmpty then
                    return {
                        Hash = hash.Value
                        Name = name
                        Tsize = tsize
                    }
                else
                    let! struct(buffer, wireType, fieldNumber) = decodeKey buffer

                    if fieldNumber = 1 then
                        do! result {
                            do! (hash |> ValueOption.isNone) |> Result.requireTrueWith (fun () -> exn "Duplicate Hash section")
                            do! (wireType = 2) |> Result.requireTrueWith (fun () -> exn $"Wrong wireType (%i{wireType}) for Hash")
                            do! (name |> ValueOption.isNone) |> Result.requireTrueWith (fun () -> exn "Invalid order, found Name before Hash")
                            do! (tsize |> ValueOption.isNone) |> Result.requireTrueWith (fun () -> exn "Invalid order, found Tsize before Hash")
                        }
                        let! buffer, hashBuffer = decodeBytes buffer
                        let cidRes, _consumed = CidParser.TryParseFromSpan(hashBuffer.Span)
                        let! cid = cidRes |> Result.mapError (fun err -> AggregateException("Failed parse Hash", err) :> exn)
                        return! loop buffer (ValueSome cid) name tsize
                    elif fieldNumber = 2 then
                        do! result {
                            do! (name |> ValueOption.isNone) |> Result.requireTrueWith (fun () -> exn "Duplicate Name section")
                            do! (wireType = 2) |> Result.requireTrueWith (fun () -> exn $"Wrong wireType (%i{wireType}) for Name")
                            do! (tsize |> ValueOption.isNone) |> Result.requireTrueWith (fun () -> exn "Invalid order, found Tsize before Name")
                        }
                        let! buffer, nameBuffer = decodeBytes buffer
                        let name = Encoding.UTF8.GetString(nameBuffer.Span) // TODO: Add try-catch
                        return! loop buffer hash (ValueSome name) tsize
                    elif fieldNumber = 3 then
                        do! result {
                            do! (tsize |> ValueOption.isNone) |> Result.requireTrueWith (fun () -> exn "Duplicate Tsize section")
                            do! (wireType = 0) |> Result.requireTrueWith (fun () -> exn $"Wrong wireType (%i{wireType}) for Tsize")
                        }
                        let! buffer, tsize = VarInt.parseFromMemoryAsUInt64 buffer
                        return! loop buffer hash name (ValueSome tsize)
                    else
                        return! Error ^ exn $"invalid fieldNumber, expected 1, 2 or 3, got {fieldNumber}"
            }
        loop buffer ValueNone ValueNone ValueNone

    let decodeNode (buffer: ReadOnlyMemory<byte>) : Result<PBNode, exn> =
        let rec loop
                (buffer: ReadOnlyMemory<byte>)
                (links: ResizeArray<PBLink> voption) (linksBeforeData: bool) (data: ReadOnlyMemory<byte> voption)
                : Result<PBNode, exn> =
            result {
                if buffer.IsEmpty then
                    return {
                        Links = links |> ValueOption.map _.ToArray() |> ValueOption.defaultValue [| |]
                        Data = data
                    }
                else
                    let! buffer, wireType, fieldNumber = decodeKey buffer
                    if wireType <> 2 then
                        return! Error ^ exn $"Invalid wireType, expected 2, got {wireType}"
                    elif fieldNumber = 1 then
                        if data |> ValueOption.isSome then
                            return! Error ^ exn "Duplicate Data sections"
                        else
                            let! buffer, dataBuffer = decodeBytes buffer
                            let data = ReadOnlyMemory(dataBuffer.ToArray())
                            let linksBeforeData = links |> ValueOption.isSome
                            return! loop buffer links linksBeforeData (ValueSome data)
                    elif fieldNumber = 2 then
                        if linksBeforeData then // interleaved Links/Data/Links
                            return! Error ^ exn "Duplicate Links sections"
                        else
                            let links = if links |> ValueOption.isNone then ResizeArray() |> ValueSome else links
                            let! buffer, linkBuffer = decodeBytes buffer
                            let! link = decodeLink linkBuffer
                            links.Value.Add(link)
                            return! loop buffer links linksBeforeData data
                    else
                        return! Error ^ exn $"Invalid fieldNumber, expected 1 or 2, got {fieldNumber}"
            }
        loop buffer ValueNone false ValueNone

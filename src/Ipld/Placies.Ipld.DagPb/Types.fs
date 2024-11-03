namespace Placies.Ipld.DagPb

open System
open System.Collections.Generic
open Placies

// Based on https://github.com/ipld/js-dag-pb/blob/27ed1722e3d1788e40f03186a1dc9b1ba69fd1d2/src/interface.ts

type PBLink = {
    Name: string voption
    Tsize: uint64 voption
    Hash: Cid
}

type PBNode = {
    Data: ReadOnlyMemory<byte> voption
    Links: IReadOnlyList<PBLink>
}

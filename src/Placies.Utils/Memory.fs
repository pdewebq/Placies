namespace Placies.Utils

open System

[<AutoOpen>]
module ReadOnlyMemoryExtensions =
    type Memory<'T> with
        member this.AsReadOnly(): ReadOnlyMemory<'T> =
            Memory.op_Implicit(this)

[<AutoOpen>]
module ReadOnlySpanExtensions =
    type Span<'T> with
        member this.AsReadOnly(): ReadOnlySpan<'T> =
            Span.op_Implicit(this)

[<RequireQualifiedAccess>]
module Array =
    let inline asMemory (array: 'a array) : Memory<'a> = array.AsMemory()
    let inline asReadOnlyMemory (array: 'a array) : ReadOnlyMemory<'a> = array.AsMemory().AsReadOnly()

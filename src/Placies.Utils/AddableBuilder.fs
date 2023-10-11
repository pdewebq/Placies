namespace Placies.Utils

open System.Runtime.CompilerServices


[<AutoOpen; Extension>]
type AddableExtensions =
    [<Extension>]
    static member inline Yield<'TAddable, 'T when 'TAddable : (member Add: 'T -> unit)>(this: 'TAddable, x: 'T): unit =
        this.Add(x)
    [<Extension>]
    static member inline YieldFrom<'TAddable, 'T when 'TAddable : (member Add: 'T -> unit)>(this: 'TAddable, xs: 'T seq): unit =
        for x in xs do
            this.Add(x)
    [<Extension>]
    static member inline Combine<'TAddable, 'T when 'TAddable : (member Add: 'T -> unit)>(_this: 'TAddable, (), ()): unit =
        ()
    [<Extension>]
    static member inline Delay<'TAddable, 'T when 'TAddable : (member Add: 'T -> unit)>(_this: 'TAddable, f: unit -> unit): unit =
        f ()
    [<Extension>]
    static member inline Zero<'TAddable, 'T when 'TAddable : (member Add: 'T -> unit)>(_this: 'TAddable): unit =
        ()
    [<Extension>]
    static member inline For<'TAddable, 'T, 'U when 'TAddable : (member Add: 'T -> unit)>(_this: 'TAddable, source: 'U seq, action: 'U -> unit): unit =
        for x in source do
            action x
    [<Extension>]
    static member inline Run<'TAddable, 'T when 'TAddable : (member Add: 'T -> unit)>(this: 'TAddable, ()): 'TAddable =
        this

[<AutoOpen; Extension>]
type AddableExtensionsHighPriority =
    [<Extension>]
    static member inline YieldFrom<'TAddable, 'T when 'TAddable : (member AddRange: 'T seq -> unit)>(this: 'TAddable, xs: 'T seq): unit =
        this.AddRange(xs)

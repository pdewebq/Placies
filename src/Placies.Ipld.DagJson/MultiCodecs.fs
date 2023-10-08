namespace Placies.Ipld

open Ipfs.Registry

module ShipyardMultiCodec =

    let registerMore () =

        Codec.Register("dag-json", 0x0129) |> ignore

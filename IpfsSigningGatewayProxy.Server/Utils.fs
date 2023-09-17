[<AutoOpen>]
module IpfsSigningGatewayProxy.Server.Utils

open Microsoft.AspNetCore.Http


type Results with
    static member Unauthorized(data: obj): IResult =
        { new IResult with
            member _.ExecuteAsync(ctx: HttpContext) = task {
                ctx.Response.StatusCode <- StatusCodes.Status401Unauthorized
                do! ctx.Response.WriteAsJsonAsync(data)
            }
        }

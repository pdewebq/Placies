module Placies.Gateways.AllowlistProxyGateway.Server.Utils

open Microsoft.AspNetCore.Http


type Results with
    static member Gone(data: obj): IResult =
        { new IResult with
            member _.ExecuteAsync(ctx: HttpContext) = task {
                ctx.Response.StatusCode <- StatusCodes.Status410Gone
                do! ctx.Response.WriteAsJsonAsync(data)
            }
        }

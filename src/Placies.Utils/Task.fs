namespace Placies.Utils

open System.Threading.Tasks

[<RequireQualifiedAccess>]
module Task =

    let runSynchronously (task: Task<'a>) : 'a =
        task.GetAwaiter().GetResult()

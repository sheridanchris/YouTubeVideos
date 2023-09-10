open Giraffe
open Giraffe.Htmx
open Giraffe.ViewEngine
open Giraffe.ViewEngine.Htmx
open Microsoft.AspNetCore.Builder

let router: HttpHandler =
  choose [
    DataTable.handler
    ValidatedForm.handler
  ]

let webApplicationBuilder = WebApplication.CreateBuilder()
webApplicationBuilder.Services.AddGiraffe() |> ignore

let webApp = webApplicationBuilder.Build()
webApp.UseGiraffe router
webApp.Run()

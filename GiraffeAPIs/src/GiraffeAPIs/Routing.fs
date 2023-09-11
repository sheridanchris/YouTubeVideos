module Routing

open Giraffe

let router: HttpHandler =
  choose [
    route "/" >=> GET >=> text "Hello, World!"
    route "/ping" >=> GET >=> text "Pong!"
    routeStartsWith "/secure"
    >=> requiresAuthentication (setStatusCode 401)
    >=> choose [
      route "/secure/admin"
      >=> requiresRole "Admin" (setStatusCode 403)
      >=> text "Super secret!"
    ]
  ]

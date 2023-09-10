module DataTable

open Giraffe
open Giraffe.Htmx
open Giraffe.ViewEngine
open Giraffe.ViewEngine.Htmx

[<CLIMutable>]
type PersonalInformation = {
  Id: int
  FirstName: string
  LastName: string
  ContactEmail: string
}

[<CLIMutable>]
type EditPersonalInformationCommand = {
  FirstName: string
  LastName: string
  ContactEmail: string
}

// This is only mutable because I'm simulating a db... or something.
let mutable infos = [
  {
    Id = 1
    FirstName = "John"
    LastName = "Doe"
    ContactEmail = "john@site.com"
  }
  {
    Id = 2
    FirstName = "Jane"
    LastName = "Doe"
    ContactEmail = "jane@site.com"
  }
]

let private renderPersonalInformation (personalInformation: PersonalInformation) =
  tr [] [
    td [] [ str personalInformation.FirstName ]
    td [] [ str personalInformation.LastName ]
    td [] [ str personalInformation.ContactEmail ]
    td [] [
      button [
        _hxGet $"/infos/edit/{personalInformation.Id}"
        _hxTarget "closest tr"
        _hxSwap "outerHTML"
      ] [ str "Edit" ]
    ]
  ]

let private renderEditablePersonalInformation (personalInformation: PersonalInformation) =
  tr [] [
    td [] [
      input [
        _id "firstName"
        _name "firstName"
        _value personalInformation.FirstName
      ]
    ]
    td [] [
      input [
        _id "lastName"
        _name "lastName"
        _value personalInformation.LastName
      ]
    ]
    td [] [
      input [
        _id "contactEmail"
        _name "contactEmail"
        _value personalInformation.ContactEmail
      ]
    ]
    td [] [
      button [
        _hxPut $"/infos/edit/{personalInformation.Id}"
        _hxSwap "outerHTML"
        _hxTarget "closest tr"
        _hxInclude "closest tr"
      ] [ str "Save" ]
      button [
        _hxGet $"/infos/{personalInformation.Id}"
        _hxSwap "outerHTML"
        _hxTarget "closest tr"
      ] [ str "Cancel" ]
    ]
  ]

let private initialPage informationList =
  html [] [
    head [ _title "DataTable example" ] [ Script.minified ]
    main [ _id "content" ] [
      table [] [
        tr [] [
          th [] [ str "First Name" ]
          th [] [ str "Last Name" ]
          th [] [ str "Contact Email" ]
          th [] [ str "Actions" ]
        ]
        yield! informationList |> List.map renderPersonalInformation
      ]
    ]
  ]

let editPersonInformationHandler
  (personalInformation: PersonalInformation)
  (command: EditPersonalInformationCommand)
  : HttpHandler =
  fun next ctx ->
    let newPersonalInfo = {
      personalInformation with
          FirstName = command.FirstName
          LastName = command.LastName
          ContactEmail = command.ContactEmail
    }

    infos <-
      infos
      |> List.map (fun info -> if info = personalInformation then newPersonalInfo else info)

    htmlView (renderPersonalInformation newPersonalInfo) next ctx

let handler: HttpHandler =
  choose [
    route "/infos" >=> GET >=> htmlView (initialPage infos)
    GET
    >=> routef "/infos/%i" (fun id ->
      let currentPersonalInformation = List.tryFind (fun info -> info.Id = id) infos

      match currentPersonalInformation with
      | None -> RequestErrors.NOT_FOUND "Not found" // this should probably return html.
      | Some currentPersonalInformation -> currentPersonalInformation |> renderPersonalInformation |> htmlView)
    routef "/infos/edit/%i" (fun id ->
      let currentPersonalInformation = List.tryFind (fun info -> info.Id = id) infos

      match currentPersonalInformation with
      | None -> RequestErrors.NOT_FOUND "Not found" // this should probably return html.
      | Some currentPersonalInformation ->
        choose [
          GET
          >=> (currentPersonalInformation |> renderEditablePersonalInformation |> htmlView)

          PUT
          >=> bindForm<EditPersonalInformationCommand> None (editPersonInformationHandler currentPersonalInformation)
        ])
  ]

module ValidatedForm

open System
open System.Net.Mail
open Validus
open Validus.Operators
open Giraffe
open Giraffe.Htmx
open Giraffe.ViewEngine
open Giraffe.ViewEngine.Htmx

[<CLIMutable>]
type AccountCreationForm = {
  Username: string
  EmailAddress: string
  Password: string
}

[<CLIMutable>]
type ValidateUsernameCommand = { Username: string }

[<CLIMutable>]
type ValidateEmailAddressCommand = { EmailAddress: string }

[<CLIMutable>]
type ValidatePasswordCommand = { Password: string }

let stringExists (f: char -> bool) (message: ValidationMessage) =
  let rule = Seq.exists f
  Validator.create message rule

let isEmailAddress (message: ValidationMessage) =
  let rule = MailAddress.TryCreate >> fst
  Validator.create message rule

let isSymbol (value: char) =
  let symbols = "!@#$%^&*()-_=+'\";:/?.>,<|\\"
  symbols.Contains value

type ValidUsername = ValidUsername of string
type ValidEmailAddress = ValidEmailAddress of string
type ValidPassword = ValidPassword of string

let usernameValidator =
  Check.String.notEmpty
  <+> Check.String.greaterThanLen 3
  <+> Check.String.lessThanLen 50
  <+> Check.WithMessage.String.pattern "^[a-zA-Z0-9]*$" (sprintf "%s must only contain letters and numbers.")
  >>| ValidUsername

let emailAddressValidator =
  Check.String.notEmpty
  <+> Check.String.lessThanLen 100
  <+> isEmailAddress (sprintf "%s must be a valid.")
  >>| ValidEmailAddress

let passwordValidator =
  Check.String.notEmpty
  <+> Check.String.greaterThanOrEqualToLen 6
  <+> stringExists Char.IsUpper (sprintf "%s must contain an uppercase character.")
  <+> stringExists Char.IsLower (sprintf "%s must conntain a lowercase character.")
  <+> stringExists isSymbol (sprintf "%s must contain a symbol.")
  >>| ValidPassword

type ValidatedAccountCreateForm = {
  Username: ValidUsername
  EmailAddress: ValidEmailAddress
  Password: ValidPassword
}

let usernameKey = "Username"
let emailAddressKey = "Email address"
let passwordKey = "Password"

let validateForm (form: AccountCreationForm) : ValidationResult<ValidatedAccountCreateForm> =
  validate {
    let! validUsername = usernameValidator usernameKey form.Username
    let! validEmailAddress = emailAddressValidator emailAddressKey form.EmailAddress
    let! validPassword = passwordValidator passwordKey form.Password

    return {
      Username = validUsername
      EmailAddress = validEmailAddress
      Password = validPassword
    }
  }

let validationClassName errors =
  if List.isEmpty errors then "color good" else "color bad"

let renderErrors errors =
  errors
  |> List.tryHead
  |> Option.map (fun value -> [ span [ _class "color bad" ] [ str value ] ])
  |> Option.defaultValue []

let inputValueOrPlaceholder placeholder value =
  if String.IsNullOrWhiteSpace value then
    _placeholder placeholder
  else
    _value value

let usernameFormControl (usernameValue: string) (validationErrors: string list) =
  p [
    _hxTarget "this"
    _hxSwap "outerHTML"
  ] [
    label [ _for "username" ] [ str "Username" ]
    input [
      _id "username"
      _name "username"
      _type "text"
      _hxPost "/accounts/validation/username"
      _required
      _class (validationClassName validationErrors)
      inputValueOrPlaceholder "username" usernameValue
    ]
    yield! renderErrors validationErrors
  ]

let emailAddressFormControl (emailValue: string) (validationErrors: string list) =
  p [
    _hxTarget "this"
    _hxSwap "outerHTML"
  ] [
    label [ _for "emailAddress" ] [ str "Email Address" ]
    input [
      _id "emailAddress"
      _name "emailAddress"
      _type "email"
      _hxPost "/accounts/validation/emailAddress"
      _required
      _class (validationClassName validationErrors)
      inputValueOrPlaceholder "email@site.com" emailValue
    ]
    yield! renderErrors validationErrors
  ]

let passwordFormControl (passwordValue: string) (validationErrors: string list) =
  p [
    _hxTarget "this"
    _hxSwap "outerHTML"
  ] [
    label [ _for "password" ] [ str "Password" ]
    input [
      _id "password"
      _name "password"
      _type "password"
      _hxPost "/accounts/validation/password"
      _required
      _class (validationClassName validationErrors)
      inputValueOrPlaceholder "********" passwordValue
    ]
    yield! renderErrors validationErrors
  ]

let createAccountForm (values: Map<string, string>) (validationErrrors: Map<string, string list>) =
  let valueFor key =
    values |> Map.tryFind key |> Option.defaultValue ""

  let validationErrorsFor key =
    validationErrrors |> Map.tryFind key |> Option.defaultValue []

  form [
    _hxPost "/accounts/create"
    _hxTarget "this"
    _hxSwap "outerHTML"
  ] [
    fieldset [] [
      legend [] [ str "Create an Account." ]
      usernameFormControl (valueFor usernameKey) (validationErrorsFor usernameKey)
      emailAddressFormControl (valueFor emailAddressKey) (validationErrorsFor emailAddressKey)
      passwordFormControl (valueFor passwordKey) (validationErrorsFor passwordKey)
      button [ _type "submit" ] [ str "Create" ]
    ]
  ]

let createAccountPage =
  html [] [
    head [ _title "Validated form." ] [
      Script.minified
      link [
        _rel "stylesheet"
        _href "https://unpkg.com/missing.css@1.0.9/dist/missing.min.css"
      ]
    ]
    body [] [ main [] [ createAccountForm Map.empty Map.empty ] ]
  ]

// this might be too generic and hard to understand!
let validationHandler
  (key: string)
  (value: string)
  (validator: Validator<string, _>)
  (view: string -> string list -> XmlNode)
  : HttpHandler =
  match validator key value with
  | Ok _ -> htmlView (view value [])
  | Error validationErrors ->
    validationErrors
    |> ValidationErrors.toMap
    |> Map.tryFind key
    |> Option.defaultValue List.empty
    |> view value
    |> htmlView

let processAccountCreationForm (form: AccountCreationForm) : HttpHandler =
  match validateForm form with
  | Ok _ -> text "Account was created."
  | Error validationErrors ->
    let errorsMap = ValidationErrors.toMap validationErrors

    let valuesMap =
      Map.ofList [
        usernameKey, form.Username
        emailAddressKey, form.EmailAddress
        passwordKey, form.Password
      ]

    htmlView (createAccountForm valuesMap errorsMap)

let handler: HttpHandler =
  choose [
    route "/accounts/create"
    >=> choose [
      GET >=> htmlView createAccountPage
      POST >=> bindForm None processAccountCreationForm
    ]

    route "/accounts/validation/username"
    >=> bindForm<ValidateUsernameCommand> None (fun command ->
      validationHandler usernameKey command.Username usernameValidator usernameFormControl)

    route "/accounts/validation/emailAddress"
    >=> bindForm<ValidateEmailAddressCommand> None (fun command ->
      validationHandler emailAddressKey command.EmailAddress emailAddressValidator emailAddressFormControl)

    route "/accounts/validation/password"
    >=> bindForm<ValidatePasswordCommand> None (fun command ->
      validationHandler passwordKey command.Password passwordValidator passwordFormControl)
  ]

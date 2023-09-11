type Reason =
  | Completed
  | Duplicate
  | WillNotFix
  | Spam
  | Other of string

type Status =
  | Unresolved
  | Resolved of Reason

type Ticket = {
  Id: int
  Title: string
  Description: string
  Author: string
  Status: Status
}

type State = {
  Tickets: Ticket list
  NextTicketId: int
}

type Command =
  | CreateTicket of Ticket
  | Update of Ticket
  | Delete of int
  | Resolve of int * Reason

type Event =
  | TicketCreated of Ticket
  | TicketUpdated of Ticket
  | TicketedDeleted of int

let interpret state command =
  match command with
  | CreateTicket ticket -> [ TicketCreated { ticket with Id = state.NextTicketId } ]
  | Update updatedTicket ->
    match state.Tickets |> List.tryFind (fun ticket -> ticket.Id = updatedTicket.Id) with
    | Some ticket when ticket <> updatedTicket -> [ TicketUpdated updatedTicket ]
    | _ -> []
  | Delete ticketId ->
    if state.Tickets |> List.exists (fun ticket -> ticket.Id = ticketId) then
      [ TicketedDeleted ticketId ]
    else
      []
  | Resolve(ticketId, reason) ->
    match state.Tickets |> List.tryFind (fun ticket -> ticket.Id = ticketId) with
    | Some ticket when ticket.Status = Unresolved -> [ TicketUpdated { ticket with Status = Resolved reason } ]
    | _ -> []

let evolve state event =
  match event with
  | TicketCreated ticket -> {
      state with
          Tickets = ticket :: state.Tickets
          NextTicketId = state.NextTicketId + 1
    }
  | TicketUpdated updatedTicket -> {
      state with
          Tickets =
            state.Tickets
            |> List.map (fun ticket ->
              if ticket.Id = updatedTicket.Id then
                updatedTicket
              else
                ticket)
    }
  | TicketedDeleted deletedTicketId -> {
      state with
          Tickets = state.Tickets |> List.filter (fun ticket -> ticket.Id <> deletedTicketId)
    }

let initialState = {
  Tickets = []
  NextTicketId = 1
}

let fold state = Seq.fold evolve state
let replay events = fold initialState events

// example.
let command =
  CreateTicket {
    Id = 0
    Title = "This is a title"
    Description = "This is a description"
    Author = "John Doe"
    Status = Unresolved
  }

let events = interpret initialState command
let newState = fold initialState events

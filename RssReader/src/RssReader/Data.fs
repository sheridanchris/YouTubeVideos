module Data

open System
open Npgsql
open SqlFun
open SqlFun.NpgSql
open SqlFun.Queries
open SqlFun.GeneratorConfig

type Feed = {
  Name: string
  Url: string
}

type Post = {
  Feed: string
  Title: string
  Url: string
  PublishedAt: DateTime
  UpdatedAt: DateTime
}

let connectionString =
  "User ID=postgres;Password=password;Host=localhost;Port=5432;Database=postgres"

let createConnection () = new NpgsqlConnection(connectionString)
let generatorConfig = createDefaultConfig createConnection

let sql commandText = sql generatorConfig commandText
let proc name = proc generatorConfig name

let run f = DbAction.run createConnection f
let runAsync f = AsyncDb.run createConnection f

let getFeeds: unit -> AsyncDb<Feed list> = sql "select * from feed"
let getPosts: unit -> AsyncDb<Post list> = sql "select * from post"

let getPostsByFeed: string -> AsyncDb<Post list> =
  sql "select * from post where feed = @feed"

let lastPostPublishedAt: unit -> AsyncDb<DateTime option> =
  sql "select publishedAt from post order by publishedAt desc limit 1"

let searchPosts: string -> AsyncDb<Post list> =
  sql "select * from post where to_tsvector(title) @@ phraseto_tsquery(@title)"

let insertPosts (posts: Post list) =
  BulkCopy.WriteToServer posts |> runAsync

open Giraffe
open Giraffe.Htmx
open Giraffe.ViewEngine
open Giraffe.ViewEngine.Htmx
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open FSharp.Data
open System
open System.Threading.Tasks
open Npgsql
open DbUp
open System.Data.Common
open System.Data
open Microsoft.Extensions.Hosting
open System.Threading
open Data

DeployChanges.To
  .PostgresqlDatabase(Data.connectionString)
  .WithScriptsFromFileSystem("Migrations")
  .Build()
  .PerformUpgrade()
|> ignore

[<Literal>]
let rssSample =
  """
  <rss>
    <channel>
      <item>
        <title>Title</title>
        <link>Link</link>
        <pubDate>Sun, 11 Dec 2022 19:10:01 GMT</pubDate>
      </item>
      <item>
        <title>Title</title>
        <link>Link</link>
      </item>
    </channel>
  </rss>
  """

type RSS = XmlProvider<rssSample>

let readPostsFromFeed (feed: Feed) =
  task {
    let! result = RSS.AsyncLoad feed.Url

    // Here, we only return items that have a published date so we don't accidentally get duplicates.
    return
      result.Channel.Items
      |> Array.choose (fun item ->
        let publishedAt = item.PubDate |> Option.map (fun v -> v.DateTime)

        match publishedAt with
        | None -> None
        | Some publishedAt ->
          Some {
            Feed = feed.Name
            Title = item.Title
            Url = item.Link
            PublishedAt = publishedAt
            UpdatedAt = publishedAt
          })
      |> Array.toList
  }

let renderPost post =
  let publishedAt = post.PublishedAt.ToLongDateString()
  let updatedAt = post.UpdatedAt.ToLongDateString()

  tr [] [
    td [] [ str post.Feed ]
    td [] [ a [ _href post.Url ] [ str post.Title ] ]
    td [] [ str publishedAt ]
    td [] [ str updatedAt ]
  ]

let feedsView (posts: Post list) =
  table [ _id "feeds" ] [
    tr [] [
      th [] [ str "Name" ]
      th [] [ str "Title" ]
      th [] [ str "Posted" ]
      th [] [ str "Updated" ]
    ]
    yield! posts |> List.map renderPost
  ]

let page (posts: Post list) =
  let feeds = posts |> List.map (fun post -> post.Feed) |> List.distinct

  html [] [
    head [ _title "Rss Reader" ] [
      Script.minified
      link [
        _rel "stylesheet"
        _href "https://unpkg.com/missing.css@1.0.9/dist/missing.min.css"
      ]
    ]
    body [] [
      select [
        _name "feed"
        _hxGet "/feeds"
        _hxTarget "#feeds"
      ] [
        for feed in feeds do
          option [ _value feed ] [ str feed ]
      ]
      input [
        _name "searchQuery"
        _placeholder "search for a title"
        _hxPost "/search"
        _hxTrigger "keyup changed delay:500ms, searchQuery"
        _hxTarget "#feeds"
      ]
      feedsView posts
    ]
  ]

let postsView: HttpHandler =
  fun next ctx ->
    task {
      let! posts = getPosts () |> runAsync
      return! htmlView (page posts) next ctx
    }

let specificFeedView (feedName: string) : HttpHandler =
  fun next ctx ->
    task {
      let! posts = getPostsByFeed feedName |> runAsync
      return! htmlView (feedsView posts) next ctx
    }

[<CLIMutable>]
type Search = { SearchQuery: string }

let searchForFeedsHandler (value: Search) : HttpHandler =
  fun next ctx ->
    task {
      if String.IsNullOrWhiteSpace value.SearchQuery then
        let! posts = getPosts () |> runAsync
        return! htmlView (feedsView posts) next ctx
      else
        let! posts = searchPosts value.SearchQuery |> runAsync
        return! htmlView (feedsView posts) next ctx
    }

let tryBindQueryString (queryParam: string) (success: string -> HttpHandler) (failure: HttpHandler) : HttpHandler =
  fun next ctx ->
    match ctx.TryGetQueryStringValue queryParam with
    | None -> failure next ctx
    | Some value -> success value next ctx

type MyBackgroundService() =
  inherit BackgroundService()

  let delayInMinutes = 5

  let filterNew (latest: DateTime option) (posts: seq<Post>) =
    match latest with
    | None -> posts
    | Some latest -> Seq.filter (fun post -> post.PublishedAt > latest) posts

  override _.ExecuteAsync(ct: CancellationToken) =
    task {
      while not ct.IsCancellationRequested do
        let now = DateTime.UtcNow
        let! latestPost = lastPostPublishedAt () |> runAsync

        let! feeds = getFeeds () |> runAsync
        let! posts = feeds |> List.map readPostsFromFeed |> Task.WhenAll

        let uniquePosts =
          posts
          |> Seq.collect id
          |> Seq.distinctBy (fun post -> post.Url)
          |> filterNew latestPost
          |> Seq.toList

        do! insertPosts uniquePosts
        do! Task.Delay(TimeSpan.FromMinutes delayInMinutes)
    }

let router =
  choose [
    route "/" >=> GET >=> postsView
    route "/feeds" >=> GET >=> tryBindQueryString "feed" specificFeedView postsView
    route "/search" >=> POST >=> bindForm None searchForFeedsHandler
  ]

let webApplication = WebApplication.CreateBuilder()
webApplication.Services.AddGiraffe() |> ignore
webApplication.Services.AddHostedService<MyBackgroundService>() |> ignore
let app = webApplication.Build()
app.UseGiraffe router

app.Run()

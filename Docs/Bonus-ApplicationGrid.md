# Bonus: Paged, Sortable Application List

The application list now pages and sorts, and SQL Server does both. The table is a reusable **data grid** view component that loads its rows from a JSON endpoint, `GET /api/applications`. The endpoint returns one page of rows plus the number of applications that match the filters, and it's described in an OpenAPI document.

## How it works

- **Sort by any column.** Click a header to sort by it; click it again to flip the direction. The arrow and `aria-sort` show which column is sorted and which way. Until you pick one, it's newest application first, as before.
- **Pages of 10, 25, 50 or 100.** The pager shows the first page, the last page and two on either side of the current one, with "1–10 of 32" next to it.
- **Filters work as before, without reloading the page.** Submitting the status/property form reloads just the grid, starting from page 1. Clear still goes back to the unfiltered list. As with a plain form, changing a dropdown does nothing until you press Filter, even if you page or sort in between.
- **The URL keeps your place.** Page, page size, sort and filters are written into the query string (defaults left out), so a refresh, a bookmark, or Back from an application brings you to the same page of the same list.
- **Same people see the same rows.** Applicants see the applications they're on and managers see everything that's been submitted, because the endpoint goes through the same `ApplicationService` query (`Visible`) as before.

## Paging and sorting in the database

`ApplicationService.ListAsync(ApplicationListQuery, CurrentUser)` runs two queries:

1. `SELECT COUNT(*)` over the filtered rows, which becomes the total.
2. The page itself, with `ORDER BY ... OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY`, projected straight into `ApplicationListItemViewModel`. Only one page of rows ever leaves SQL Server.

| Sort key | ORDER BY |
|---|---|
| `Id` (default, descending) | application id |
| `Property` | property name, unit number, id |
| `Applicant` | email on the application (or the profile's, if not saved yet), id |
| `Status` | the `Status` lookup table's name, id |
| `Submitted` | submitted time (never-submitted drafts first ascending), id |

- **The id is always the last key.** Without a unique tiebreaker, SQL Server can return rows that tie (say, twenty "Approved") in a different order on each query, so a row could show up on two pages or on none. A test walks every page of a sort with ties and checks each application appears exactly once.
- **A page past the end returns the last page.** That happens if the filters change, or if applications are withdrawn after the page loaded. The response's `page` says which page came back, and the grid follows it.
- **The service clamps the page size (1–100) itself.** The endpoint already rejects bad values, but the service doesn't rely on its callers to keep the query small.
- **The count and the page aren't one snapshot.** If someone submits between the two queries, the total can be off by one until the next load. For a list that's fine, and it avoids holding a transaction open.

## The data grid component

`<vc:data-grid grid="..." />` (`ViewComponents/DataGridViewComponent.cs`) takes a `DataGridViewModel`: the endpoint URL, the columns, the default sort, the page sizes and, optionally, the id of a filter form. Its view (`Views/Shared/Components/DataGrid/Default.cshtml`) renders the table header, the pager and the rows-per-page picker, and puts the settings in a `data-grid` attribute as JSON. `wwwroot/js/grid.js` does the rest.

Each column is set up in the page:

```csharp
new() { Key = "Id", Title = "#", Field = "id", Href = Url.Action("Edit") + "/{id}" },
new() { Key = "Property", Title = "Property / unit", Template = "{propertyName} · {unitNumber}" },
new() { Key = "Submitted", Title = "Submitted", Field = "submittedAt", Format = DataGridFormat.Date }
```

- `Key` is what goes in `sort=`, so it has to be a value the endpoint accepts.
- `Field` is a JSON property. `Template` combines several, and `Href` makes the cell a link (its values are URL-encoded).
- `Format = Date` shows the date part as the server sent it, with no time-zone shift, the same as the old `ToShortDateString()`.

Another list only needs an endpoint that takes `page`, `pageSize`, `sort` and `dir` and returns a `PagedResult<T>`. It needs no new markup or JavaScript.

**Rendering is text only.** Every value goes in with `textContent`, and hrefs are built from URL-encoded values, so an email or property name can never be read as HTML. Like `site.js`, `grid.js` has no framework, lives inside an IIFE, and sends `X-Requested-With`. If a response comes back out of order, only the newest one is drawn. If the user has signed out in the meantime, the page reloads so the login page comes up.

## The endpoint

`GET /api/applications` (`Controllers/Api/ApplicationsApiController.cs`)

| Query | Values | Default |
|---|---|---|
| `status` | `Draft`, `Submitted`, `Returned`, `Approved`, `Denied`, `Withdrawn`, `UnderReview` (or the number) | all |
| `propertyId` | property id | all |
| `sort` | `Id`, `Property`, `Applicant`, `Status`, `Submitted` | `Id` |
| `dir` | `Asc`, `Desc` | `Desc` |
| `page` | 1 or more | 1 |
| `pageSize` | 1–100 | 10 |

```json
{
  "items": [
    { "id": 42, "propertyName": "Domenica Isle Commons", "unitNumber": "101",
      "applicant": "applicant11@example.com", "status": "Submitted", "statusName": "Submitted",
      "submittedAt": "2026-07-19T05:18:42.407" }
  ],
  "total": 32,
  "page": 1,
  "pageSize": 10
}
```

- **200:** the page and the filtered total.
- **400:** validation problem details, for an unknown `sort`, a `pageSize` over 100 and so on. `[ApiController]` sends it before the action runs.
- **401 / 403:** not signed in, or not in either role. These have no body.
- **Same cookie as the site.** The endpoint uses the Identity cookie. For paths under `/api`, the cookie handler answers 401/403 instead of redirecting to the login page (`Program.ApiStatusOr`); the pages still redirect as before.
- **Enums are sent as names** (`"UnderReview"`), so clients don't depend on the numbers. `statusName` is the display text (`"Under Review"`).
- **It's a GET, so it has no antiforgery token and changes nothing.**

## OpenAPI

In Development, the document is at **`/openapi/v1.json`**. It's generated by `Microsoft.AspNetCore.OpenApi` from the API controller's routes, its `[ProducesResponseType]`s and its XML doc comments. The project now sets `GenerateDocumentationFile`; CS1591/CS1573 are suppressed because not every member is documented. It describes the query parameters (with their ranges and enum values), the response schema, the error responses and the cookie security scheme. The MVC pages use conventional routing, so they don't appear in it.

- **Development only.** Like the migrations endpoint, it's only mapped in Development.
- **Enums use the same names in the document and on the wire.** The OpenAPI generator reads the minimal-API JSON options, so `JsonStringEnumConverter` is registered there as well as on MVC's.
- **Accurate error responses.** An operation transformer removes the ProblemDetails body MVC would list for 401/403, because the cookie handler sends those responses with no body.

## Tests

`ApplicationServiceTests` has new tests for:

- the page and the filtered total, including a status filter
- that the paging happens in SQL: a command interceptor sees a `COUNT` and then a query with `ORDER BY`/`LIMIT`/`OFFSET` (SQLite's version of `OFFSET/FETCH`)
- a page past the end, and the no-match case
- page size clamping
- every column sorted both ways, with ties broken by id
- walking every page of a sort with ties, checking each row appears exactly once

The existing list tests now call the new signature.

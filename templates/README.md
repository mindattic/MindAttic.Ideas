# MindAttic.Ideas project templates

Scaffold a new modular `.idea` citizen with `dotnet new`.

```bash
# install the template (once)
dotnet new install ./templates/maidea-page

# scaffold a new page (run from samples/ so the ..\..\src Abstractions path resolves).
# -n is the SHORT name (the namespace tail / key); it becomes MindAttic.Ideas.Page.<Name>.
cd samples
dotnet new maidea-page -n MyPage --slug my-page --theme cyberspace
#   -> samples/MyPage/  with MindAttic.Ideas.Page.MyPage.csproj, namespace MindAttic.Ideas.Page.MyPage, class V1
```

The generated project mirrors [`samples/MindAttic.Ideas.Page.HelloWorld`](../samples/MindAttic.Ideas.Page.HelloWorld):
a Razor Class Library that compiles against **only** the Abstractions SDK and composes its theme +
components **by string id**. Then follow [`docs/AUTHORING.md`](../docs/AUTHORING.md) to build, pack, and
upload.

> `maidea-page` is the Page template. Component / Theme / Control templates follow the same shape (change
> the base type, the namespace `Kind`, and drop the `data/page.json` for non-Page kinds).

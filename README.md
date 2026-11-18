# Soccer Cards ⚽🃏

Футболна игра с карти за един играч срещу компютъра — изцяло на английски. Първа лига (Bulgarian First League), Купа на България и европейски турнири.

**Мачът:** всички 25 карти на мача са с гръб нагоре в решетка 5×5. Който е на ход, обръща карта и тя остава обърната: **Pass / Corner / Free Kick** — обръща пак същият;
**Tackle / Offside / Save / Miss / Yellow / Red Card** — ред на другия; **Goal!** — гол; **Penalty** — мини игра (ляво/център/дясно);
**Half Time** — полувреме; **Full Time** — край на мача (може да е първата обърната карта, може и последната). Няма минути — само 1-во и 2-ро полувреме.
Чист късмет — силата на отборите влияе само на целите на борда в кариерата.

- **Friendly match** — всеки отбор срещу всеки (16 български и 63 европейски клуба).
- **Career** — поемете клуб, изпълнете целите на борда, играйте за титлата, купата и в Европа, получавайте оферти от други клубове.
- **Load game** — кариерите се записват автоматично в базата данни.
- **Tables & teams**, **Settings**, **How to play**, **Team & league editor**.

Пълните правила на играта (на английски) са в [docs/rules.md](docs/rules.md).

## Технологии

| Част | Технология |
|---|---|
| Клиент | React 19 + TypeScript + Vite (`client/`) |
| Сървър | C# / ASP.NET Core Web API, .NET 10 (`server/FootballCards.Api`) |
| База данни | EF Core + SQLite (файл `soccer-cards.db`, създава се автоматично) |
| Тестове | xUnit (`server/FootballCards.Tests`) — правилата на картите |

## Изисквания

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) 20 или по-нов

## Стартиране (режим на разработка)

Отворете два терминала.

**1. Сървър** (http://localhost:5080):

```bash
cd server/FootballCards.Api
dotnet run
```

При първото стартиране се създава базата `soccer-cards.db` и се зареждат отборите, турнирите и картите от `Data/Seed/seed.json`.

**2. Клиент** (http://localhost:5173):

```bash
cd client
npm install
npm run dev
```

Отворете http://localhost:5173 в браузъра. Клиентът препраща заявките към `/api` към сървъра.

## Стартиране като едно приложение

```bash
cd client
npm install
npm run build
cd ../server/FootballCards.Api
dotnet run
```

Ако папката `client/dist` съществува, сървърът я обслужва сам — играта е на http://localhost:5080.

## Тестове

```bash
cd server
dotnet test
```

## Структура на проекта

```
client/                     React клиент
  src/api.ts                типове и заявки към сървъра
  src/pages/                екрани: меню, приятелски мач, мач, кариера, записи, класирания, редактор, настройки, правила
  src/components/           емблеми, карти, таблици
server/
  FootballCards.Api/
    Data/Entities/          таблици: Leagues, Teams, Competitions, Cards, Careers, CareerSeasons, Fixtures, CareerMessages, Matches
    Data/Seed/seed.json     начални данни (отбори, турнири, карти)
    Game/                   двигател на мача (обръщане на карти, дузпи), коментари
    Careers/                кариера: график, симулация, борд, оферти
    Endpoints/              REST API
  FootballCards.Tests/      тестове
docs/rules.md               правила на играта
```

## Добавяне на отбори и лиги

**През играта:** Главно меню → *Редактор на отбори и лиги*. Можете да добавяте/редактирате отбори (име, град, стадион, цветове, рейтинги) и лиги.

**През файла:** допишете отбори в `server/FootballCards.Api/Data/Seed/seed.json` и рестартирайте сървъра — липсващите отбори и лиги (по име) се добавят автоматично, без да се изтриват промените, направени през редактора. Пример за нова лига:

```json
{
  "name": "Second League",
  "country": "Bulgaria",
  "tier": 2,
  "isPlayable": true,
  "teams": [
    { "name": "Hebar Pazardzhik", "shortName": "HEB", "city": "Pazardzhik", "stadium": "Georgi Benkovski Stadium",
      "primaryColor": "#2a6fd6", "secondaryColor": "#ffffff", "attack": 56, "midfield": 56, "defense": 57 }
  ]
}
```

Лига с `isPlayable: true` и поне 4 отбора (препоръчително четен брой) може да се избира за кариера.
Отборите без лига (`europeanClubs`) са съперниците в европейските турнири.

**През API:** `POST /api/teams`, `PUT /api/teams/{id}`, `DELETE /api/teams/{id}`, `POST /api/leagues` и т.н.

## Смяна на базата данни

По подразбиране се използва SQLite (без инсталация). Настройката е в `server/FootballCards.Api/appsettings.json`:

```json
"Database": { "Provider": "Sqlite", "ConnectionString": "Data Source=soccer-cards.db" }
```

За SQL Server или PostgreSQL добавете съответния пакет (`Microsoft.EntityFrameworkCore.SqlServer` или
`Npgsql.EntityFrameworkCore.PostgreSQL`), разкоментирайте реда в `Program.cs` и сменете `Provider` и `ConnectionString`.
Схемата се създава автоматично при първо стартиране (`EnsureCreated`). За да започнете на чисто със SQLite, просто изтрийте файла `soccer-cards.db`. (Старият файл `football-cards.db` от първата версия вече не се ползва и може да се изтрие.)

## Бележки

- Отборите в Първа лига са по състава за сезон 2025/26; ако съставът се промени, обновете го през редактора или `seed.json`.
- Рейтингите на отборите са приблизителна игрова оценка; засега влияят само на целите на борда.
- Емблемите се генерират от цветовете на клуба — играта не използва реални логота или търговски марки.
- Европейските турнири са с общи имена: Champions Cup, Europa Cup, Conference Cup.
- Съставът на решетката (брой копия на всяка карта) е в секцията `cards` на `seed.json`; синхронизира се с базата при всяко стартиране.
- Режим за разработка с друг порт на сървъра: в PowerShell `$env:API_URL="http://localhost:5090"; npm run dev`.

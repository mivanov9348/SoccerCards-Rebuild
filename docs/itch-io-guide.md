# Как да качите Dugout Deck в itch.io

itch.io е безплатен: няма такса за качване и няма одобрение. По подразбиране взима 10% от продажбите, а процентът се настройва (Open Revenue Sharing, от 0% до 100%). Добро място за първа версия и за обратна връзка, преди Steam.

## 1. Подготовка (веднъж)

1. **Пакетът.** От главната папка на проекта:
   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts\publish-desktop.ps1
   ```
   Резултатът е `dist\DugoutDeck-1.0.0-win-x64.zip`, около 50 MB.
2. **Тест на чист компютър.** Разархивирайте zip-а на друг компютър (или в друг Windows акаунт) и пуснете `DugoutDeck.exe`. Изиграйте обучението и един мач.
3. **Снимки (3–5)**, 1280×720 или по-големи: главното меню, решетката по време на мач, надписът GOAL!, тактиката, таблицата в кариерата. В играта: F11 за цял екран, после Win+Shift+S.
4. **Корица:** 630×500 px (минимум 315×250). Например логото върху зеления фон на играта и надпис „Dugout Deck“.
5. **Акаунт:** регистрирайте се в itch.io. В Settings → „Payment“ попълнете PayPal или Stripe. Данъчната форма (Tax interview, за България W-8BEN) се попълва, преди да получите пари.

## 2. Нов проект

Dashboard → **Create new project**:

| Поле | Какво да изберете |
|---|---|
| Title | Dugout Deck |
| Project URL | `dugout-deck` |
| Short description | A football card game: flip the cards, read the hints, steal your rival's best cards. |
| Classification | Games |
| Kind of project | Downloadable |
| Release status | Released (или „In development“, ако искате да е ранна версия) |
| Pricing | Paid → цена (виж т. 4), отметка „Allow people to pay more than the minimum“ |
| Uploads | качете zip-а → отметнете **Windows** |
| Genre | Card Game (втори таг: Sports) |
| Tags | football, soccer, card-game, singleplayer, management, deck-building, offline |
| AI generation disclosure | Отговорете честно. Кодът е писан с помощта на AI (Claude). Графиките, звуците и музиката са генерирани от кода на играта, не са снимки или записи. |
| Custom noun | game |
| Community | Comments (за обратна връзка) |
| Visibility | Draft → проверете страницата → Public |

## 3. Текст за страницата (на английски, копирайте)

> **Dugout Deck** is a football card game for one player.
>
> Every half is a 5×5 grid of face-down cards. Flip a Pass and you keep the ball, flip a Tackle and it's the other team's turn. Somewhere in the grid are your four team cards, and the computer's. Play yours before they're stolen, and steal the opponent's Striker if you find it. Just don't walk into their Offside Trap.
>
> **Features**
> - Quick matches with real decisions: read the hints, time your cards, play the mini-games (corners, free kicks, penalties with a timing bar).
> - Build a tactic before every match: field cards, hand cards and secret modifiers.
> - Career mode: 35 leagues in 25 countries, promotion and relegation, national cups and three continental cups.
> - Money, card packs, a store and club upgrades. Get job offers from bigger clubs.
> - Tutorial, three difficulty levels, fully offline, no ads, no microtransactions.
>
> **Controls:** mouse · F11 fullscreen · M sound on/off
>
> **Requirements:** Windows 10 or 11 (64-bit). Nothing else to install.
>
> **Windows SmartScreen:** the game is not code-signed yet, so Windows may say "Windows protected your PC". Click **More info → Run anyway**.
>
> All clubs and competitions are fictional.

## 4. Цена

Подобни малки картови и мениджърски игри в itch.io обикновено струват $3–8. Цените в itch.io са в долари.

**Препоръката ми:**

- **Цена $4.99** с отметка „Allow people to pay more“.
- **Отстъпка 30% за първата седмица** (Edit game → Sales), т.е. $3.49. Дава повод хората да купят веднага.
- **Ако искате първо повече играчи** и обратна връзка, а не пари: пуснете като „In development“ за $2.99 и вдигнете цената след няколко обновления. Хората обичат да подкрепят ранни версии, но обещайте само това, което наистина ще направите.
- **Безплатно демо** може да се добави по-късно: отделен upload с ограничение, напр. само приятелски мачове. Изисква малка промяна в кода.

## 5. Обновления

**Лесният начин:** Edit game → Uploads → качете новия zip и изтрийте стария.

**По-удобният начин** е itch.io инструментът **butler**. Играчите с приложението itch получават само промените.

1. Изтеглете butler от https://itch.io/docs/butler/ и влезте с `butler login`.
2. За всяко обновление:
   ```powershell
   butler push dist\DugoutDeck-win-x64 ВАШЕТО-ИМЕ/dugout-deck:windows --userversion 1.0.1
   ```

**При нова версия:**
- сменете `<Version>` в `server\Directory.Build.props`;
- пуснете `publish-desktop.ps1`;
- качете новия пакет.

Записите на играчите се пазят: те са в `%LOCALAPPDATA%\DugoutDeck`, не в папката на играта, а при промяна на формата играта прави резервно копие.

## 6. След пускането

- Отговаряйте на коментарите. Първите играчи са най-ценната обратна връзка.
- Създайте **devlog** (Edit game → Devlog) за всяко обновление. Показва се на страницата и носи посещения.
- Подписване на `.exe`: когато има продажби, Azure Trusted Signing (около $10 на месец) маха предупреждението на SmartScreen.
- Steam: $100 такса на игра, капсули в точни размери, трейлър и около 30 дни от регистрацията до пускането. Добре е да отидете там, след като itch.io покаже интерес.

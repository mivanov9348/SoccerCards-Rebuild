// Types and requests for the C# server.

export type Side = 'Home' | 'Away'

export interface League {
  id: number
  name: string
  country: string
  tier: number
  isPlayable: boolean
  teamCount: number
}

export interface Team {
  id: number
  name: string
  shortName: string
  city: string
  country: string
  stadium: string
  primaryColor: string
  secondaryColor: string
  attack: number
  midfield: number
  defense: number
  overall: number
  leagueId: number | null
  leagueName: string | null
}

export type TeamInput = Omit<Team, 'id' | 'overall' | 'leagueName'>

export type CardKind = 'Neutral' | 'Field' | 'Hand' | 'Modifier'

export interface CardInfo {
  code: string
  name: string
  description: string
  kind: CardKind
  rarity: string
  copiesInDeck: number
  cost: number
  price: number
}

export interface Tactic {
  field: string[]
  hand: string[]
  modifiers: string[]
}

export interface TeamStats {
  flips: number
  passes: number
  corners: number
  freeKicks: number
  ballsLost: number
  saves: number
  misses: number
  yellowCards: number
  redCards: number
  penalties: number
  bigChances: number
  cardsBurned: number
  goalSources: Record<string, number>
}

export interface MatchTeam {
  id: number
  name: string
  shortName: string
  primaryColor: string
  secondaryColor: string
  goals: number
  isHuman: boolean
  rating: number
  aiLevel: string
  passStreak: number
  captain: boolean
  wall: boolean
  calm: boolean
  press: boolean
  counterFlips: number
  fieldCards: string[] | null
  fieldCount: number
  modifiers: string[] | null
  stats: TeamStats
}

export interface MatchEvent {
  id: number
  half: number
  type: string
  side: Side | null
  text: string
  card: string | null
}

export interface GridCell {
  index: number
  faceUp: boolean
  burned: boolean
  owner: Side | null
  flippedBy: Side | null
  card: string | null
  name: string | null
  peeked: boolean
  burnedByFlip: boolean
}

export interface Decision {
  kind: 'Corner' | 'FreeKick' | 'BigChance' | 'Penalty' | 'Shootout' | 'Discard'
  source: string
  attacker: Side
  attackerName: string
  role: string
  options: string[]
  needsTiming: boolean
}

export interface SetupCard {
  code: string
  name: string
  description: string
  kind: CardKind
  rarity: string
  cost: number
  owned: number | null
}

export interface MatchSetupInfo {
  budget: number
  handSize: number
  maxModifiers: number
  fieldCards: number
  lastTactic: Tactic
  cards: SetupCard[]
}

export interface MatchView {
  id: number
  fixtureId: number | null
  careerId: number | null
  competition: string
  stadium: string
  isKnockout: boolean
  phase: 'Setup' | 'Play' | 'Decision' | 'Finished'
  half: number
  turn: Side
  humanSide: Side | null
  role: 'flip' | 'wait' | 'attack' | 'defend' | 'discard' | null
  home: MatchTeam
  away: MatchTeam
  conditions: { code: string; label: string }[]
  composition: { card: string; name: string; count: number }[]
  rowHints: number[]
  colHints: number[]
  table: GridCell[]
  cardsLeft: number
  lastFlip: { cell: number; card: string; name: string; side: Side; owner: Side | null; description: string | null } | null
  hand: { code: string; name: string; description: string | null }[]
  freeScouts: number
  handPlayed: boolean
  opponentHandCount: number
  decision: Decision | null
  shootout: { side: Side; scored: boolean }[]
  homePenalties: number | null
  awayPenalties: number | null
  events: MatchEvent[]
  setup: MatchSetupInfo | null
}

export interface FixtureTeam {
  id: number
  name: string
  primaryColor: string
  secondaryColor: string
}

export interface Fixture {
  id: number
  week: number
  round: number
  stage: string
  played: boolean
  homeGoals: number | null
  awayGoals: number | null
  homePenalties: number | null
  awayPenalties: number | null
  isKnockout: boolean
  competition: string
  competitionCode: string
  competitionType: string
  home: FixtureTeam
  away: FixtureTeam
  winnerId: number | null
}

export interface StandingRow {
  position: number
  teamId: number
  name: string
  shortName: string
  primaryColor: string
  secondaryColor: string
  played: number
  won: number
  drawn: number
  lost: number
  goalsFor: number
  goalsAgainst: number
  points: number
  goalDifference: number
}

export interface SeasonSummary {
  position: number
  points: number
  league: string
  champion: string
  cupWinner: string | null
  europeResult: string | null
  expectation: string
  verdict: string
  fired: boolean
  reputationChange: number
  moneyEarned: number
  cardsWon: string[]
  promoted: string[]
  relegated: string[]
  offers: { teamId: number; name: string; league: string; rating: number }[]
}

export interface CareerView {
  id: number
  saveName: string
  managerName: string
  reputation: number
  money: number
  status: 'Active' | 'Fired'
  team: {
    id: number
    name: string
    city: string
    stadium: string
    primaryColor: string
    secondaryColor: string
    rating: number
    upgradeLevel: number
    tacticPoints: number
    handSize: number
    aiLevel: string
  }
  league: { id: number; name: string; country: string; tier: number }
  cupName: string | null
  season: {
    id: number
    year: number
    currentWeek: number
    totalWeeks: number
    finished: boolean
    expectation: string
    european: string | null
    summary: SeasonSummary | null
  }
  collectionCount: number
  thisWeekFixture: Fixture | null
  nextFixture: Fixture | null
  activeMatchId: number | null
  position: number | null
  table: StandingRow[]
  recent: Fixture[]
  messages: { id: number; year: number; week: number; title: string; body: string }[]
}

export interface CareerSlot {
  id: number
  saveName: string
  managerName: string
  team: string
  teamColor: string
  teamColor2: string
  status: string
  reputation: number
  money: number
  updatedAt: string
  year: number | null
  week: number | null
  totalWeeks: number | null
  seasonFinished: boolean | null
}

export interface CollectionCard {
  code: string
  name: string
  description: string
  kind: CardKind
  rarity: string
  cost: number
  price: number
  sellPrice: number
  owned: number
}

export interface StoreInfo {
  money: number
  packPrice: number
  upgradeLevel: number
  upgradeCost: number | null
}

export class ApiError extends Error {}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  let res: Response
  try {
    res = await fetch(`/api${url}`, {
      method,
      headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    })
  } catch {
    throw new ApiError('Cannot reach the server. Is the C# server running (dotnet run)?')
  }
  if (!res.ok) {
    let message = `Error ${res.status}`
    try {
      const data = await res.json()
      if (data?.error) message = data.error
    } catch {
      /* empty response */
    }
    throw new ApiError(message)
  }
  if (res.status === 204) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const api = {
  info: () => request<{ saveFile: string }>('GET', '/info'),
  leagues: () => request<League[]>('GET', '/leagues'),
  createLeague: (l: Omit<League, 'id' | 'teamCount'>) => request<{ id: number }>('POST', '/leagues', l),
  updateLeague: (id: number, l: Omit<League, 'id' | 'teamCount'>) => request<void>('PUT', `/leagues/${id}`, l),
  deleteLeague: (id: number) => request<void>('DELETE', `/leagues/${id}`),

  teams: (leagueId?: number) => request<Team[]>('GET', leagueId ? `/teams?leagueId=${leagueId}` : '/teams'),
  createTeam: (t: TeamInput) => request<{ id: number }>('POST', '/teams', t),
  updateTeam: (id: number, t: TeamInput) => request<void>('PUT', `/teams/${id}`, t),
  deleteTeam: (id: number) => request<void>('DELETE', `/teams/${id}`),

  cards: () => request<CardInfo[]>('GET', '/cards'),

  // matches
  createFriendly: (homeTeamId: number, awayTeamId: number, humanSide: Side) =>
    request<MatchView>('POST', '/matches', { homeTeamId, awayTeamId, humanSide }),
  match: (id: number) => request<MatchView>('GET', `/matches/${id}`),
  startMatch: (id: number, tactic: Tactic, saveAs?: string) =>
    request<MatchView>('POST', `/matches/${id}/start`, { tactic, saveAs: saveAs || null }),
  flip: (id: number, cell: number) => request<MatchView>('POST', `/matches/${id}/flip`, { cell }),
  playHand: (id: number, card: string, cell?: number, cell2?: number) =>
    request<MatchView>('POST', `/matches/${id}/hand`, { card, cell: cell ?? null, cell2: cell2 ?? null }),
  decide: (id: number, choice: number | null, timing: number | null, auto = false) =>
    request<MatchView>('POST', `/matches/${id}/decide`, { choice, timing, auto }),
  aiStep: (id: number) => request<MatchView>('POST', `/matches/${id}/ai-step`),
  simulateMatch: (id: number) => request<MatchView>('POST', `/matches/${id}/simulate`),

  // saved tactics
  tactics: (careerId?: number | null) =>
    request<{ id: number; name: string; tactic: Tactic }[]>('GET', careerId ? `/tactics?careerId=${careerId}` : '/tactics'),
  saveTactic: (careerId: number | null, name: string, tactic: Tactic) => request<{ id: number }>('POST', '/tactics', { careerId, name, tactic }),
  deleteTactic: (id: number) => request<void>('DELETE', `/tactics/${id}`),

  // careers
  careers: () => request<CareerSlot[]>('GET', '/careers'),
  createCareer: (saveName: string, managerName: string, teamId: number) =>
    request<{ id: number }>('POST', '/careers', { saveName, managerName, teamId }),
  career: (id: number) => request<CareerView>('GET', `/careers/${id}`),
  renameCareer: (id: number, saveName: string) => request<void>('PUT', `/careers/${id}`, { saveName }),
  deleteCareer: (id: number) => request<void>('DELETE', `/careers/${id}`),
  careerFixtures: (id: number) => request<Fixture[]>('GET', `/careers/${id}/fixtures`),
  careerLeagues: (id: number) => request<{ id: number; name: string; country: string; tier: number }[]>('GET', `/careers/${id}/leagues`),
  careerStandings: (id: number, leagueId: number) => request<StandingRow[]>('GET', `/careers/${id}/standings/${leagueId}`),
  playCareer: (id: number, useLastTactic: boolean) => request<{ matchId: number }>('POST', `/careers/${id}/play`, { useLastTactic }),
  simulateCareer: (id: number) => request<void>('POST', `/careers/${id}/simulate`),
  continueCareer: (id: number) => request<void>('POST', `/careers/${id}/continue`),
  nextSeason: (id: number, acceptTeamId?: number) =>
    request<void>('POST', `/careers/${id}/next-season`, { acceptTeamId: acceptTeamId ?? null }),
  collection: (id: number) => request<CollectionCard[]>('GET', `/careers/${id}/collection`),
  store: (id: number) => request<StoreInfo>('GET', `/careers/${id}/store`),
  buyPack: (id: number) => request<{ cards: string[] }>('POST', `/careers/${id}/store/pack`),
  buyCard: (id: number, code: string) => request<void>('POST', `/careers/${id}/store/buy`, { code }),
  sellCard: (id: number, code: string) => request<void>('POST', `/careers/${id}/store/sell`, { code }),
  upgradeClub: (id: number) => request<{ level: number }>('POST', `/careers/${id}/store/upgrade`),
}

export const seasonLabel = (year: number) => `${year}/${String((year + 1) % 100).padStart(2, '0')}`

export const money = (v: number) => (Math.abs(v) >= 1_000_000 ? `€${(v / 1_000_000).toFixed(1)}M` : `€${Math.round(v / 1000)}K`)

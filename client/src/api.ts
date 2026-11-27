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
  /** what each tier adds (Field and Hand cards) */
  bonuses?: string[] | null
}

export interface Tactic {
  field: string[]
  hand: string[]
  modifiers: string[]
  /** tier of each field / hand card (0 Normal, 1 Rare, 2 Ultra Rare, 3 Legendary); missing = Normal */
  fieldTiers?: number[]
  handTiers?: number[]
}

/** Card tiers (1.2). */
export const TIER_NAMES = ['Normal', 'Rare', 'Ultra Rare', 'Legendary']
export const TIER_SHORT = ['N', 'R', 'UR', 'L']

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
  cardsStolen: number
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
  goalkeeper: boolean
  targetMan: boolean
  sweeper: boolean
  twelfthMan: boolean
  var: boolean
  penaltyTaker: boolean
  /** a Marking shield is on one of my cards (only sent for my team) */
  marking?: boolean
  /** cards this team has played: how = hand, own, stolen, trap, decoy */
  played?: { code: string; name: string; half: number; how: 'hand' | 'own' | 'stolen' | 'trap' | 'decoy'; tier?: number }[]
  captainExtra?: number
  wallExtra?: number
  sweeperExtra?: number
  twelfthManExtra?: number
  calmExtra?: number
  pressExtra?: number
  goalkeeperBonus?: number
  targetManBonus?: number
  varBonus?: number
  penaltyTakerBonus?: number
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
  /** one of my own team cards (only my own are ever marked — the computer's positions are not sent) */
  mine: boolean
  /** my own face-down card protected by Marking */
  marked?: boolean
  /** a team card flipped by the other team */
  stolen: boolean
  /** tier of a team card (when known) */
  tier?: number
}

export interface Decision {
  kind: 'Corner' | 'FreeKick' | 'BigChance' | 'Penalty' | 'Shootout' | 'Discard'
  source: string
  attacker: Side
  attackerName: string
  role: string
  options: string[]
  needsTiming: boolean
  /** Discard (red card): the codes of the sent-off team's hand cards */
  cards?: string[]
  tiers?: number[]
}

export interface SetupCard {
  code: string
  name: string
  description: string
  kind: CardKind
  rarity: string
  cost: number
  owned: number | null
  /** copies per tier [Normal, Rare, Ultra Rare, Legendary] (null = unlimited, friendlies) */
  tiers?: number[] | null
  /** what each tier adds to this card */
  bonuses?: string[]
}

/** What the tactic editor needs (pre-match screen and the career's Tactics tab). */
export interface TacticEditorSetup {
  budget: number
  handSize: number
  maxModifiers: number
  fieldCards: number
  lastTactic: Tactic
  cards: SetupCard[]
}

export interface MatchSetupInfo extends TacticEditorSetup {
  /** a career's three plans (Tactics tab) and the active one */
  plans?: { active: number; list: { name: string; tactic: Tactic | null }[] } | null
}

/** The career's Tactics tab. */
export interface CareerTactics {
  active: number
  plans: { name: string; tactic: Tactic | null; cost: number; awayErrors: string[] }[]
  setup: TacticEditorSetup & { awayBudget: number }
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
  difficulty: 'Easy' | 'Normal' | 'Hard'
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
  /** the finished first half, revealed (second half only) */
  firstHalfTable: GridCell[] | null
  lastFlip: { cell: number; card: string; name: string; side: Side; owner: Side | null; description: string | null; tier?: number } | null
  hand: { code: string; name: string; description: string | null; tier?: number; bonus?: string }[]
  freeScouts: number
  handPlayed: boolean
  /** I already flipped one of my own team cards this turn */
  ownCardUsed: boolean
  opponentCardsLeft: number
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
  income?: number
  expenses?: number
  cardsWon: string[]
  promoted: string[]
  relegated: string[]
  offers: { teamId: number; name: string; league: string; rating: number }[]
}

export interface CareerView {
  id: number
  hasTactic: boolean
  /** the name of the active plan, when it has a tactic */
  activePlan: string | null
  /** the "pick 1 of 3" draft at the start of a career (null when done) */
  draft: DraftView | null
  /** money of this season (1.3) */
  finances: { income: number; expenses: number; weeklyExpenses: number; debtLimit: number }
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
    difficulty: 'Easy' | 'Normal' | 'Hard'
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
  owned: number
  tiers: { tier: number; name: string; count: number; sellPrice: number; bonus: string }[]
}

export interface StoreInfo {
  money: number
  packs: { size: number; price: number; rarePlus: boolean }[]
  /** pack prices in this league (×0.7 … ×1.5) */
  priceFactor: number
  merge: { from: number; to: number; need: number; fee: number }[]
  sellPrices: { tier: number; name: string; price: number }[]
  upgradeLevel: number
  upgradeCost: number | null
}

/** A card from a pack or the draft. */
export interface TieredCard {
  code: string
  name: string
  description?: string | null
  rarity?: string
  kind?: string
  cost?: number
  tier: number
  tierName: string
  bonus: string
}

/** The club history tab (1.3). */
export interface CareerHistory {
  seasons: {
    year: number; team: string; league: string; finished: boolean; position: number | null; points: number | null
    goalsFor: number | null; goalsAgainst: number | null; cup: boolean; europe: string | null; verdict: string | null; fired: boolean
    income: number; expenses: number
  }[]
  trophies: { kind: 'league' | 'cup' | 'europe'; name: string; year: number; team: string }[]
  record: { played: number; won: number; drawn: number; lost: number; goalsFor: number; goalsAgainst: number }
  records: { biggestWin: string | null; worstDefeat: string | null; longestUnbeaten: number; longestWinningRun: number }
  stats: {
    matchesPlayed: number
    goalSources: { source: string; goals: number }[]
    penaltiesAwarded: number; penaltiesScored: number; stolenByMe: number; stolenFromMe: number; legendaryPlays: number
    mostStolen: { code: string; name: string; count: number } | null
    favourite: { code: string; name: string; count: number } | null
  }
  achievements: { code: string; name: string; description: string; unlocked: boolean; when: string | null; reward: string }[]
}

/** Name packs (1.3). */
export interface NamePack {
  file: string
  name: string
  author: string | null
  teams: number
  leagues: number
  competitions: number
  enabled: boolean
  error: string | null
}

export interface DraftView {
  round: number
  total: number
  kind: 'Field' | 'Hand'
  options: TieredCard[]
  picked: TieredCard[]
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

/** A coloured band of a league table (positions from..to). */
export interface TableZone {
  from: number
  to: number
  kind: 'champion' | 'ucl' | 'uel' | 'uecl' | 'promotion' | 'relegation'
  label: string
}

export const api = {
  info: () => request<{ saveFile: string; version: string; desktop: boolean }>('GET', '/info'),
  leagues: () => request<League[]>('GET', '/leagues'),
  leagueZones: (id: number) => request<TableZone[]>('GET', `/leagues/${id}/zones`),
  createLeague: (l: Omit<League, 'id' | 'teamCount'>) => request<{ id: number }>('POST', '/leagues', l),
  updateLeague: (id: number, l: Omit<League, 'id' | 'teamCount'>) => request<void>('PUT', `/leagues/${id}`, l),
  deleteLeague: (id: number) => request<void>('DELETE', `/leagues/${id}`),

  teams: (leagueId?: number) => request<Team[]>('GET', leagueId ? `/teams?leagueId=${leagueId}` : '/teams'),
  createTeam: (t: TeamInput) => request<{ id: number }>('POST', '/teams', t),
  updateTeam: (id: number, t: TeamInput) => request<void>('PUT', `/teams/${id}`, t),
  deleteTeam: (id: number) => request<void>('DELETE', `/teams/${id}`),

  cards: () => request<CardInfo[]>('GET', '/cards'),

  // matches
  createFriendly: (homeTeamId: number, awayTeamId: number, humanSide: Side, difficulty: string = 'Normal') =>
    request<MatchView>('POST', '/matches', { homeTeamId, awayTeamId, humanSide, difficulty }),
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
  createCareer: (saveName: string, managerName: string, teamId: number, difficulty: string = 'Normal') =>
    request<{ id: number }>('POST', '/careers', { saveName, managerName, teamId, difficulty }),
  career: (id: number) => request<CareerView>('GET', `/careers/${id}`),
  renameCareer: (id: number, saveName: string) => request<void>('PUT', `/careers/${id}`, { saveName }),
  deleteCareer: (id: number) => request<void>('DELETE', `/careers/${id}`),
  careerFixtures: (id: number) => request<Fixture[]>('GET', `/careers/${id}/fixtures`),
  careerLeagues: (id: number) => request<{ id: number; name: string; country: string; tier: number }[]>('GET', `/careers/${id}/leagues`),
  careerTactics: (id: number) => request<CareerTactics>('GET', `/careers/${id}/tactics`),
  saveCareerPlan: (id: number, slot: number, plan: { name?: string; tactic?: Tactic }) =>
    request<CareerTactics>('PUT', `/careers/${id}/tactics/${slot}`, plan),
  setActivePlan: (id: number, slot: number) => request<CareerTactics>('POST', `/careers/${id}/tactics/active`, { slot }),
  careerStandings: (id: number, leagueId: number) => request<StandingRow[]>('GET', `/careers/${id}/standings/${leagueId}`),
  playCareer: (id: number, useLastTactic: boolean) => request<{ matchId: number }>('POST', `/careers/${id}/play`, { useLastTactic }),
  simulateCareer: (id: number) => request<void>('POST', `/careers/${id}/simulate`),
  continueCareer: (id: number) => request<void>('POST', `/careers/${id}/continue`),
  nextSeason: (id: number, acceptTeamId?: number) =>
    request<void>('POST', `/careers/${id}/next-season`, { acceptTeamId: acceptTeamId ?? null }),
  collection: (id: number) => request<CollectionCard[]>('GET', `/careers/${id}/collection`),
  store: (id: number) => request<StoreInfo>('GET', `/careers/${id}/store`),
  buyPack: (id: number, size: number) => request<{ cards: TieredCard[] }>('POST', `/careers/${id}/store/pack`, { size }),
  buyCard: (id: number, code: string) => request<void>('POST', `/careers/${id}/store/buy`, { code }),
  sellCard: (id: number, code: string, tier: number) => request<{ money: number }>('POST', `/careers/${id}/store/sell`, { code, tier }),
  sellDuplicates: (id: number) => request<{ cards: number; money: number }>('POST', `/careers/${id}/store/sell-duplicates`),
  merge: (id: number, code: string, tier: number) => request<{ tier: number }>('POST', `/careers/${id}/store/merge`, { code, tier }),
  careerHistory: (id: number) => request<CareerHistory>('GET', `/careers/${id}/history`),
  namePacks: () => request<{ folder: string; packs: NamePack[] }>('GET', '/names'),
  importNamePack: (fileName: string, content: string) => request<{ file: string; packs: NamePack[] }>('POST', '/names/import', { fileName, content }),
  enableNamePack: (file: string, enabled: boolean) => request<{ packs: NamePack[] }>('POST', `/names/${encodeURIComponent(file)}/enable`, { enabled }),
  deleteNamePack: (file: string) => request<{ packs: NamePack[] }>('DELETE', `/names/${encodeURIComponent(file)}`),
  exportNamePack: () => request<{ file: string; content: string; packs: NamePack[] }>('POST', '/names/export'),
  resetNamePacks: () => request<{ packs: NamePack[] }>('POST', '/names/reset'),
  draftPick: (id: number, pick: number) => request<{ draft: DraftView | null }>('POST', `/careers/${id}/draft`, { pick }),
  upgradeClub: (id: number) => request<{ level: number }>('POST', `/careers/${id}/store/upgrade`),
}

export const seasonLabel = (year: number) => `${year}/${String((year + 1) % 100).padStart(2, '0')}`

export const money = (v: number) => (Math.abs(v) >= 1_000_000 ? `€${(v / 1_000_000).toFixed(1)}M` : `€${Math.round(v / 1000)}K`)

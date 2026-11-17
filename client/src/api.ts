// Типове и функции за връзка с C# сървъра.

export type Side = 'Home' | 'Away'
export type Tactic = 'Balanced' | 'Attacking' | 'Defensive'

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

export interface CardInfo {
  code: string
  name: string
  description: string
  kind: 'Attack' | 'Defense'
  copiesInDeck: number
}

export interface HandCard {
  index: number
  code: string
  name: string
  legal: boolean
  hint: number
  hintLabel: string
}

export interface MatchTeam {
  id: number
  name: string
  shortName: string
  primaryColor: string
  secondaryColor: string
  goals: number
  tactic: Tactic
  isHuman: boolean
  attack: number
  midfield: number
  defense: number
  stats: { shots: number; onTarget: number; fouls: number; yellow: number; red: number; possessionTurns: number }
}

export interface MatchEvent {
  id: number
  minute: number
  minuteLabel: string
  type: string
  side: Side | null
  text: string
  attackCard: string | null
  defenseCard: string | null
}

export interface MatchView {
  id: number
  fixtureId: number | null
  careerId: number | null
  competition: string
  stadium: string
  isKnockout: boolean
  home: MatchTeam
  away: MatchTeam
  minute: number
  minuteLabel: string
  half: number
  phase: 'Play' | 'Penalty' | 'Finished'
  possession: Side
  zone: number
  zoneName: string
  counterAvailable: boolean
  humanSide: Side | null
  humanRole: 'attack' | 'defense' | 'shooter' | 'keeper'
  hand: HandCard[] | null
  basic: { code: string; name: string; hint: number; hintLabel: string } | null
  opponentHand: { attack: number; defense: number } | null
  homePenalties: number | null
  awayPenalties: number | null
  events: MatchEvent[]
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
  champion: string
  cupWinner: string | null
  europeResult: string | null
  expectation: string
  verdict: string
  fired: boolean
  reputationChange: number
  offers: { teamId: number; name: string; overall: number }[]
}

export interface CareerView {
  id: number
  saveName: string
  managerName: string
  reputation: number
  difficulty: number
  status: 'Active' | 'Fired'
  team: {
    id: number
    name: string
    city: string
    stadium: string
    primaryColor: string
    secondaryColor: string
    attack: number
    midfield: number
    defense: number
    overall: number
  }
  season: {
    id: number
    year: number
    currentWeek: number
    totalWeeks: number
    finished: boolean
    expectation: string
    european: string | null
    weekCompetition: string | null
    summary: SeasonSummary | null
  }
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
  updatedAt: string
  year: number | null
  week: number | null
  totalWeeks: number | null
  seasonFinished: boolean | null
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
    throw new ApiError('Няма връзка със сървъра. Стартиран ли е C# сървърът (dotnet run)?')
  }
  if (!res.ok) {
    let message = `Грешка ${res.status}`
    try {
      const data = await res.json()
      if (data?.error) message = data.error
    } catch {
      /* празен отговор */
    }
    throw new ApiError(message)
  }
  if (res.status === 204) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const api = {
  leagues: () => request<League[]>('GET', '/leagues'),
  createLeague: (l: Omit<League, 'id' | 'teamCount'>) => request<{ id: number }>('POST', '/leagues', l),
  updateLeague: (id: number, l: Omit<League, 'id' | 'teamCount'>) => request<void>('PUT', `/leagues/${id}`, l),
  deleteLeague: (id: number) => request<void>('DELETE', `/leagues/${id}`),

  teams: (leagueId?: number) => request<Team[]>('GET', leagueId ? `/teams?leagueId=${leagueId}` : '/teams'),
  createTeam: (t: TeamInput) => request<{ id: number }>('POST', '/teams', t),
  updateTeam: (id: number, t: TeamInput) => request<void>('PUT', `/teams/${id}`, t),
  deleteTeam: (id: number) => request<void>('DELETE', `/teams/${id}`),

  cards: () => request<CardInfo[]>('GET', '/cards'),

  createFriendly: (homeTeamId: number, awayTeamId: number, humanSide: Side, tactic: Tactic, difficulty: number) =>
    request<MatchView>('POST', '/matches', { homeTeamId, awayTeamId, humanSide, tactic, difficulty }),
  match: (id: number) => request<MatchView>('GET', `/matches/${id}`),
  play: (id: number, action: { cardIndex?: number; swapIndex?: number; penaltyDirection?: number }) =>
    request<MatchView>('POST', `/matches/${id}/play`, action),
  simulateMatch: (id: number) => request<MatchView>('POST', `/matches/${id}/simulate`),

  careers: () => request<CareerSlot[]>('GET', '/careers'),
  createCareer: (saveName: string, managerName: string, teamId: number, difficulty: number) =>
    request<{ id: number }>('POST', '/careers', { saveName, managerName, teamId, difficulty }),
  career: (id: number) => request<CareerView>('GET', `/careers/${id}`),
  renameCareer: (id: number, saveName: string) => request<void>('PUT', `/careers/${id}`, { saveName }),
  deleteCareer: (id: number) => request<void>('DELETE', `/careers/${id}`),
  careerFixtures: (id: number) => request<Fixture[]>('GET', `/careers/${id}/fixtures`),
  playCareer: (id: number, tactic: Tactic) => request<{ matchId: number }>('POST', `/careers/${id}/play`, { tactic }),
  simulateCareer: (id: number) => request<void>('POST', `/careers/${id}/simulate`),
  nextSeason: (id: number, acceptTeamId?: number) =>
    request<void>('POST', `/careers/${id}/next-season`, { acceptTeamId: acceptTeamId ?? null }),
}

export const tacticNames: Record<Tactic, string> = {
  Balanced: 'Балансирана',
  Attacking: 'Атакуваща',
  Defensive: 'Дефанзивна',
}

export const difficultyNames = ['Лесно', 'Нормално', 'Трудно']

export const seasonLabel = (year: number) => `${year}/${String((year + 1) % 100).padStart(2, '0')}`

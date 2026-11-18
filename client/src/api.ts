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

export interface CardInfo {
  code: string
  name: string
  description: string
  copiesInDeck: number
}

export interface TeamStats {
  passes: number
  tackles: number
  corners: number
  freeKicks: number
  offsides: number
  saves: number
  misses: number
  yellowCards: number
  redCards: number
  penalties: number
  flips: number
}

export interface MatchTeam {
  id: number
  name: string
  shortName: string
  primaryColor: string
  secondaryColor: string
  goals: number
  isHuman: boolean
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

export interface MatchView {
  id: number
  fixtureId: number | null
  careerId: number | null
  competition: string
  stadium: string
  isKnockout: boolean
  home: MatchTeam
  away: MatchTeam
  half: number
  phase: "Play" | "Penalty" | "Shootout" | "Finished"
  turn: Side
  humanSide: Side | null
  humanRole: "flip" | "wait" | "shooter" | "keeper" | null
  penaltyShooter: Side | null
  table: { index: number; faceUp: boolean; card: string | null; name: string | null; flippedBy: Side | null }[]
  cardsLeft: number
  lastFlip: { cell: number; card: string; name: string; side: Side } | null
  shootout: { side: Side; scored: boolean }[]
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
  leagues: () => request<League[]>('GET', '/leagues'),
  createLeague: (l: Omit<League, 'id' | 'teamCount'>) => request<{ id: number }>('POST', '/leagues', l),
  updateLeague: (id: number, l: Omit<League, 'id' | 'teamCount'>) => request<void>('PUT', `/leagues/${id}`, l),
  deleteLeague: (id: number) => request<void>('DELETE', `/leagues/${id}`),

  teams: (leagueId?: number) => request<Team[]>('GET', leagueId ? `/teams?leagueId=${leagueId}` : '/teams'),
  createTeam: (t: TeamInput) => request<{ id: number }>('POST', '/teams', t),
  updateTeam: (id: number, t: TeamInput) => request<void>('PUT', `/teams/${id}`, t),
  deleteTeam: (id: number) => request<void>('DELETE', `/teams/${id}`),

  cards: () => request<CardInfo[]>('GET', '/cards'),

  createFriendly: (homeTeamId: number, awayTeamId: number, humanSide: Side) =>
    request<MatchView>('POST', '/matches', { homeTeamId, awayTeamId, humanSide }),
  match: (id: number) => request<MatchView>('GET', `/matches/${id}`),
  flip: (id: number, cell: number) => request<MatchView>('POST', `/matches/${id}/flip`, { cell }),
  aiFlip: (id: number) => request<MatchView>('POST', `/matches/${id}/ai-flip`),
  penalty: (id: number, direction: number) => request<MatchView>('POST', `/matches/${id}/penalty`, { direction }),
  simulateMatch: (id: number) => request<MatchView>('POST', `/matches/${id}/simulate`),

  careers: () => request<CareerSlot[]>('GET', '/careers'),
  createCareer: (saveName: string, managerName: string, teamId: number) =>
    request<{ id: number }>('POST', '/careers', { saveName, managerName, teamId }),
  career: (id: number) => request<CareerView>('GET', `/careers/${id}`),
  renameCareer: (id: number, saveName: string) => request<void>('PUT', `/careers/${id}`, { saveName }),
  deleteCareer: (id: number) => request<void>('DELETE', `/careers/${id}`),
  careerFixtures: (id: number) => request<Fixture[]>('GET', `/careers/${id}/fixtures`),
  playCareer: (id: number) => request<{ matchId: number }>('POST', `/careers/${id}/play`),
  simulateCareer: (id: number) => request<void>('POST', `/careers/${id}/simulate`),
  nextSeason: (id: number, acceptTeamId?: number) =>
    request<void>('POST', `/careers/${id}/next-season`, { acceptTeamId: acceptTeamId ?? null }),
}

export const seasonLabel = (year: number) => `${year}/${String((year + 1) % 100).padStart(2, '0')}`

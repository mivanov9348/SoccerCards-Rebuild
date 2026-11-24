import { useState } from 'react'
import { api, money, TIER_NAMES, type CareerView, type DraftView, type TieredCard } from '../api'
import { ErrorBox, useLoad } from './common'
import { CardFace, CardThumb, PackOpening, TierBadge } from './GameCard'
import { play } from '../sound'

/**
 * The club's cards (1.2): every card with its copies per tier (Normal / Rare / Ultra Rare / Legendary) and what each tier
 * adds. The market is always open: any copy can be sold — cheaply, so selling is for clearing duplicates, not for profit.
 */
export function CollectionTab({ careerId, reload }: { careerId: number; reload: () => void }) {
  const { data: cards, reload: reloadCards } = useLoad(() => api.collection(careerId), [careerId])
  const [error, setError] = useState<string | null>(null)
  const [note, setNote] = useState<string | null>(null)
  const [showMissing, setShowMissing] = useState(false)
  const act = async (fn: () => Promise<string>) => {
    setError(null)
    try {
      setNote(await fn())
      await reloadCards()
      reload()
    } catch (e) {
      setError((e as Error).message)
    }
  }
  const dupes = (cards ?? []).reduce((s, c) => s + Math.max(0, (c.tiers[0]?.count ?? 0) - 1), 0)
  const shown = (cards ?? []).filter((c) => showMissing || c.owned > 0)
  return (
    <div className="panel">
      <div className="coll-head">
        <p className="muted">
          The cards belong to the club: if you take another job, they stay here and you get the new club&apos;s squad cards. Selling is
          cheap — Normal {money(15_000)}, Rare {money(60_000)}, Ultra Rare {money(250_000)}, Legendary {money(1_000_000)}.
        </p>
        <div className="row-buttons">
          <button
            className="btn"
            disabled={dupes === 0}
            onClick={() =>
              act(async () => {
                const r = await api.sellDuplicates(careerId)
                return `Sold ${r.cards} duplicate Normal card(s) for ${money(r.money)}.`
              })
            }
          >
            Sell all duplicates ({dupes} Normal)
          </button>
          <label className="checkbox small">
            <input type="checkbox" checked={showMissing} onChange={(e) => setShowMissing(e.target.checked)} /> show missing cards
          </label>
        </div>
      </div>
      {note && <p className="good small">{note}</p>}
      <ErrorBox error={error} />
      <div className="collection">
        {shown.map((card) => {
          const best = [...card.tiers].reverse().find((t) => t.count > 0)?.tier ?? 0
          return (
            <div key={card.code} className={`coll-card tier-edge-${best} ${card.owned === 0 ? 'missing' : ''}`}>
              <span className="tb-icon">
                <CardThumb code={card.code} />
              </span>
              <b>{card.name}</b>
              <span className="muted small">
                {card.kind === 'Field' ? 'Field card' : 'Hand card'} · {card.cost} pt
              </span>
              <span className="small">{card.description}</span>
              <div className="tier-rows">
                {card.tiers
                  .filter((t) => t.count > 0)
                  .map((t) => (
                    <div key={t.tier} className="tier-row">
                      <TierBadge tier={t.tier} />
                      {t.tier === 0 && <span className="tier-badge tier-0">N</span>}
                      <span>
                        × {t.count}
                        {t.bonus && <small className="muted"> · {t.bonus}</small>}
                      </span>
                      <button
                        className="btn btn-small"
                        onClick={() =>
                          act(async () => {
                            if (t.tier >= 2 && !confirm(`Sell your ${TIER_NAMES[t.tier]} ${card.name} for ${money(t.sellPrice)}?`)) return ''
                            const r = await api.sellCard(careerId, card.code, t.tier)
                            return `Sold ${TIER_NAMES[t.tier]} ${card.name} for ${money(r.money)}.`
                          })
                        }
                      >
                        Sell {money(t.sellPrice)}
                      </button>
                    </div>
                  ))}
                {card.owned === 0 && <span className="muted small">not in the collection</span>}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}

/** The store: packs of 3 / 5 / 10 cards, club upgrades and single cards (Normal copies). */
export function StoreTab({ careerId, c, reload }: { careerId: number; c: CareerView; reload: () => void }) {
  const { data: store, reload: reloadStore } = useLoad(() => api.store(careerId), [careerId])
  const { data: cards, reload: reloadCards } = useLoad(() => api.collection(careerId), [careerId])
  const [error, setError] = useState<string | null>(null)
  const [opened, setOpened] = useState<TieredCard[] | null>(null)
  const act = async (fn: () => Promise<unknown>) => {
    setError(null)
    try {
      await fn()
      await Promise.all([reloadStore(), reloadCards()])
      reload()
    } catch (e) {
      setError((e as Error).message)
    }
  }
  if (!store) return <div className="panel muted">Loading…</div>
  return (
    <div>
      <ErrorBox error={error} />
      <div className="panel">
        <h2>Card packs</h2>
        <p className="muted">
          Every card is Normal (80%), Rare (16%), Ultra Rare (3.2%) or Legendary (0.8%). Higher tiers play better: +4 / +8 / +12% for
          cards with a chance, an extra use for the others. Bigger packs guarantee at least one Rare or better.
        </p>
        <div className="pack-offers">
          {store.packs.map((p) => (
            <div key={p.size} className={`pack-offer size-${p.size}`}>
              <span className="pack-offer-cards">
                {Array.from({ length: Math.min(p.size, 5) }, (_, i) => (
                  <i key={i} style={{ transform: `rotate(${(i - (Math.min(p.size, 5) - 1) / 2) * 8}deg)` }} />
                ))}
              </span>
              <b>{p.size} cards</b>
              <span className="muted small">{p.rarePlus ? 'at least 1 Rare+' : 'no guarantee'} · {money(Math.round(p.price / p.size))} per card</span>
              <button className="btn btn-primary" disabled={store.money < p.price} onClick={() => act(async () => setOpened((await api.buyPack(careerId, p.size)).cards))}>
                Buy for {money(p.price)}
              </button>
            </div>
          ))}
        </div>
        {opened && <PackOpening cards={opened} title={`Your pack of ${opened.length}`} onClose={() => setOpened(null)} />}
      </div>
      <div className="two-col">
        <div className="panel">
          <h2>Club upgrade — level {store.upgradeLevel}/10</h2>
          <p className="muted">
            +2 rating for {c.team.name} (now {c.team.rating}): more tactic points, more hand cards, a smarter computer when you simulate,
            and more income.
          </p>
          {store.upgradeCost != null ? (
            <button className="btn btn-primary" disabled={store.money < store.upgradeCost} onClick={() => act(() => api.upgradeClub(careerId))}>
              Upgrade for {money(store.upgradeCost)}
            </button>
          ) : (
            <p>Fully upgraded.</p>
          )}
        </div>
        <div className="panel">
          <h2>Selling</h2>
          <p className="muted">The market is always open. Sell any copy in the Collection tab:</p>
          <ul className="sell-prices">
            {store.sellPrices.map((s) => (
              <li key={s.tier}>
                <span className={`tier-text-${s.tier}`}>{s.name}</span> {money(s.price)}
              </li>
            ))}
          </ul>
        </div>
      </div>
      <div className="panel">
        <h2>Single cards</h2>
        <p className="muted">Normal copies at a fixed price: more expensive, but you pick exactly what you want. Only in-game money.</p>
        <div className="collection">
          {cards?.map((card) => (
            <div key={card.code} className="coll-card">
              <span className="tb-icon">
                <CardThumb code={card.code} />
              </span>
              <b>{card.name}</b>
              <span className="muted small">owned {card.owned}</span>
              <span className="small">{card.description}</span>
              <button className="btn btn-small" disabled={store.money < card.price} onClick={() => act(() => api.buyCard(careerId, card.code))}>
                Buy {money(card.price)}
              </button>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

/**
 * The draft at the start of a career: 10 rounds of "pick 1 of 3" (field cards first, then hand cards). The three cards of a
 * round come out face down and turn over one after another; their backs glow by tier.
 */
export function DraftPanel({ careerId, draft, onDone }: { careerId: number; draft: DraftView; onDone: () => void }) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [current, setCurrent] = useState<DraftView>(draft)
  const pick = async (i: number) => {
    setBusy(true)
    setError(null)
    try {
      const r = await api.draftPick(careerId, i)
      const t = current.options[i].tier
      play(t >= 2 ? 'packEpic' : 'pack')
      if (r.draft) setCurrent(r.draft)
      else onDone()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }
  return (
    <div className="panel draft-panel">
      <div className="draft-head">
        <div>
          <h2>
            Draft — round {current.round} of {current.total}
          </h2>
          <p className="muted">
            Pick one of three {current.kind === 'Field' ? 'field cards' : 'hand cards'} for your club ({current.round <= 6 ? 'rounds 1–6: field cards' : 'rounds 7–10: hand cards'}).
            These are your club&apos;s first cards; more come from packs, rewards and the store.
          </p>
        </div>
        <div className="draft-progress">
          {Array.from({ length: current.total }, (_, i) => (
            <i key={i} className={i < current.round - 1 ? 'done' : i === current.round - 1 ? 'now' : ''} />
          ))}
        </div>
      </div>
      <ErrorBox error={error} />
      <div className="draft-options" key={current.round}>
        {current.options.map((o, i) => (
          <button key={o.code + i} className={`draft-pick tier-hint-${o.tier}`} disabled={busy} style={{ animationDelay: `${i * 120}ms` }} onClick={() => pick(i)}>
            <CardFace code={o.code} name={o.name} description={o.description} size="normal" tier={o.tier} />
            <span className={`draft-tier tier-text-${o.tier}`}>
              {o.tierName}
              {o.bonus && <small> · {o.bonus}</small>}
            </span>
            <span className="muted small">{o.cost} tactic pt</span>
            <span className="btn btn-primary small">Pick</span>
          </button>
        ))}
      </div>
      {current.picked.length > 0 && (
        <div className="draft-picked">
          <span className="muted small">Your picks:</span>
          {current.picked.map((p, i) => (
            <span key={i} className="chip">
              <CardThumb code={p.code} /> {p.name} <TierBadge tier={p.tier} />
            </span>
          ))}
        </div>
      )}
    </div>
  )
}

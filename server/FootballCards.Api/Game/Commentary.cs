namespace FootballCards.Api.Game;

/// <summary>Match commentary. {T} = the team the line is about, {O} = the opponent, {score} = the score.</summary>
public static class Commentary
{
    private static readonly Dictionary<string, string[]> Lines = new()
    {
        ["kickoff"] = ["The referee blows the whistle — {T} kick off!", "We're under way! {T} start with the ball."],
        ["kickoff2"] = ["The cards are shuffled for the second half — {T} start.", "Second half under way with a fresh grid — {T} kick off."],
        ["PASS"] = ["Neat passing from {T}.", "{T} keep the ball moving.", "Lovely one-two from {T}!"],
        ["TACKLE"] = ["Strong tackle — {O} win the ball!", "{O} snatch it back with a crunching challenge."],
        ["OFFSIDE"] = ["The flag is up — {T} caught offside.", "Offside! The attack of {T} breaks down."],
        ["SAVE"] = ["Shot from {T}... saved by the {O} keeper!", "Great stop by the {O} goalkeeper!"],
        ["MISS"] = ["{T} shoot... and it's wide!", "Over the bar from {T}!"],
        ["YELLOW_CARD"] = ["Yellow card for a {T} player."],
        ["second_yellow"] = ["Second yellow for {T} — that's a RED CARD!"],
        ["RED_CARD"] = ["RED CARD! A {T} player is sent off!"],
        ["GOAL"] = ["GOAL! {T} score! {score}", "It's in the net! Goal for {T}! {score}", "What a finish! {T} find the net! {score}"],
        ["HALF_TIME"] = ["Half time! {score}"],
        ["FULL_TIME"] = ["FULL TIME! {score}"],
        ["CORNER"] = ["Corner kick for {T}."],
        ["FREE_KICK"] = ["Free kick to {T} in a dangerous position."],
        ["PENALTY"] = ["PENALTY to {T}! The referee points to the spot!"],
        ["big_chance"] = ["Three passes in a row — BIG CHANCE for {T}!", "{T} carve the defence open — big chance!"],
        ["burned"] = ["{T} flip a {O} card — it's burned!", "{T} burn one of {O}'s cards."],
        ["trap"] = ["Offside trap! {T} walk straight into it and lose the ball."],
        ["wall"] = ["The {O} wall holds — the goal is denied!"],
        ["press_extra"] = ["{T} press immediately and win another flip!"],
        ["captain"] = ["The {T} captain shrugs off the tackle!"],
        ["calm"] = ["{T} stay calm under the tackle and keep the ball."],
        ["counter_ignore"] = ["On the counter-attack {T} ride the danger!"],
    };

    public static string Get(string key, Random rng, string team, string opponent, string score = "")
    {
        var options = Lines.TryGetValue(key, out var l) ? l : [key];
        return options[rng.Next(options.Length)]
            .Replace("{T}", team).Replace("{O}", opponent).Replace("{score}", score);
    }
}

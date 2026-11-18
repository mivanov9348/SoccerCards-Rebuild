namespace FootballCards.Api.Game;

/// <summary>Match commentary. {T} = the team that flipped the card, {O} = the opponent.</summary>
public static class Commentary
{
    private static readonly Dictionary<string, string[]> Lines = new()
    {
        ["kickoff"] = ["The referee blows the whistle — {T} kick off!", "We're under way! {T} start with the ball."],
        ["PASS"] = ["Neat passing from {T}.", "{T} keep the ball moving.", "Lovely one-two from {T}!", "{T} work it forward patiently."],
        ["TACKLE"] = ["Strong tackle — {O} win the ball!", "{O} snatch it back with a crunching challenge.", "Possession lost by {T}."],
        ["CORNER"] = ["Corner kick for {T}.", "Deflected out — {T} have a corner."],
        ["FREE_KICK"] = ["Free kick to {T} after a foul.", "{T} are fouled and win a free kick."],
        ["OFFSIDE"] = ["The flag is up — {T} caught offside.", "Offside! The attack of {T} breaks down."],
        ["SAVE"] = ["Shot from {T}... saved by the {O} keeper!", "Great stop by the {O} goalkeeper!"],
        ["MISS"] = ["{T} shoot... and it's wide!", "Over the bar from {T}!", "So close! {T} drag it just past the post."],
        ["GOAL"] = ["GOAL! {T} score! {score}", "It's in the net! Goal for {T}! {score}", "What a finish! {T} find the net! {score}"],
        ["YELLOW_CARD"] = ["Yellow card for a {T} player.", "The referee books a player from {T}."],
        ["RED_CARD"] = ["RED CARD! A {T} player is sent off!"],
        ["PENALTY"] = ["PENALTY to {T}! The referee points to the spot!"],
        ["penalty_goal"] = ["The penalty is converted — goal for {T}! {score}"],
        ["penalty_saved"] = ["SAVED! The {O} keeper guesses right and stops the penalty!"],
        ["penalty_miss"] = ["The {T} penalty taker blazes it wide!"],
        ["HALF_TIME"] = ["Half time! {score}"],
        ["kickoff2"] = ["The second half is under way — {T} start."],
        ["FULL_TIME"] = ["FULL TIME! {score}"],
        ["full_time_early"] = ["FULL TIME already — the match ends in the first half! {score}", "The referee blows for FULL TIME early! {score}"],
    };

    public static string Get(string key, Random rng, string team, string opponent, string score = "")
    {
        var options = Lines.TryGetValue(key, out var l) ? l : [key];
        return options[rng.Next(options.Length)]
            .Replace("{T}", team).Replace("{O}", opponent).Replace("{score}", score);
    }
}

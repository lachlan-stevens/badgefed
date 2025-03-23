using System.Security.Cryptography;
using System.Text;
using ActivityPubDotNet.Core;
using BadgeFed.Models;

namespace BadgeFed.Services;

public class NotesService
{
    public static string GetLinkUniqueHash(string input)
    {
        using (MD5 md5 = MD5.Create())
        {
            // Convert the input string to a byte array and compute the hash.
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            // Convert the byte array to a hexadecimal string representation.
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2")); // "x2" means hexadecimal with two digits.
            }

            return sb.ToString();
        }
    }

    public static ActivityPubNote GetNote(
        string domain, 
        string content, 
        string url, 
        string attributedTo,
        IEnumerable<string> to,
        IEnumerable<string> cc,
        IEnumerable<ActivityPubNote.Tag> tags)
    {
        var note = new ActivityPubNote
        {
            Id = url,
            Type = "Note",
            Content = content,
            Url = url,
            AttributedTo = attributedTo,
            To = to.ToList(),
            Cc = cc.ToList(),
            Published = DateTime.UtcNow,
            Tags = tags.ToList(),
            Replies = new ActivityPubNote.Collection
            {
                Id = $"{url}/comments/",
                Type = "Collection",
                First = new ActivityPubNote.CollectionPage
                {
                    Type = "CollectionPage",
                    Next = $"{url}/comments/?page=true",
                    PartOf = $"{url}/comments/",
                    Items = new List<string>()
                }
            }
        };

        return note;
    }

    public static string GetMention(string name, string link, List<ActivityPubNote.Tag> tags)
    {
        if (name.StartsWith("@"))
            name = name.Substring(1);

        // Add mention to the tags list if not already present
        var tag = new ActivityPubNote.Tag { Type = "Mention", Href = link, Name = $"@{name}" };
        if (!tags.Any(t => t.Href == link))
        {
            tags.Add(tag);
        }

        return $"<a href=\"{link}\" class=\"u-url mention\">@<span>{name}</span></a>";
    }

    public static ActivityPubNote GetPrivateBadgeNotificationNote(BadgeRecord record)
    {
        var id = $"badge/notification/{record.Id}";
        var actor = record.Actor;
        
        var tags = new List<ActivityPubNote.Tag>();

        var to = new List<string> 
        {
            record.IssuedToSubjectUri
        };

        var cc = new List<string> 
        {
            record.Actor.Uri?.ToString()!
        };

        //TODO: it would be cool to accept the badge simply with a reply

        var acceptUrl = $"https://{record.Actor.Domain}/accept/grant/{record.Id}/{record.AcceptKey}";
        var acceptLink = $"<a href=\"{acceptUrl}\">{acceptUrl}</a>";

        var url = $"https://{record.Actor.Domain}/{id}";
       
        var note = GetNote(
            domain: actor.Domain,
            content: @$"<p>{GetMention(record.IssuedToName, record.IssuedToSubjectUri, tags)}</p>
            
            <p>You have been awarded the {record.Badge.Title} badge!</p>
            <p>Go to {acceptLink} to accept it.</p>
            
            <p>This is a private notification. Please do not share this link.</p>",

            url: url,
            attributedTo: actor.Uri?.ToString()!,
            to: to,
            cc: cc,
            tags: tags);

        return note;
    }

    public static ActivityPubNote GetBadgeNote(BadgeRecord record)
    {
        string template = @"<h1>{BadgeTitle}</h1>
        
        <p>The verified {BadgeType} was issued to {IssuedTo}</p>

        <p><strong>{BadgeDescription}</strong></p>

        <p>Earning Criteria: {EarningCriteria}. <br />Issued on: {IssuedOn}<br />Accepted On: {AcceptedOn}</p>
        
        <p>Verify the {BadgeType} at <a href='{BadgeUrl}'>{BadgeUrl}</a></p>
        ";

        var url = $"https://{record.Actor.Domain}/record/{record.Id}";

        var tags = new List<ActivityPubNote.Tag>();

        var content = template
            .Replace("{BadgeType}", record.Badge.BadgeType)
            .Replace("{BadgeTitle}", record.Title)
            .Replace("{BadgeDescription}", record.Description)
            .Replace("{EarningCriteria}", record.EarningCriteria)
            .Replace("{IssuedOn}", record.IssuedOn.ToString())
            .Replace("{ActorFediverseHandle}", GetMention(record.Actor.FediverseHandle, record.Actor.Uri.ToString(), tags))
            .Replace("{AcceptedOn}", record.AcceptedOn?.ToString() ?? "Not accepted")
            .Replace("{IssuedTo}", GetMention(record.IssuedToName, record.IssuedToSubjectUri, tags))
            .Replace("{BadgeUrl}", url);

        var actor = record.Actor;
        
        var to = new List<string> 
        {
            "https://www.w3.org/ns/activitystreams#Public",
            record.IssuedToSubjectUri
        };

        var cc = new List<string> 
        {
            record.IssuedToSubjectUri
        };

        var note = GetNote(
            domain: actor.Domain,
            content: content,
            url: url,
            attributedTo: actor.Uri?.ToString()!,
            to: to,
            cc: cc,
            tags: tags);

        note.BadgeMetadata = new BadgeRecord();
        
        return note;
    }

    public static ActivityPubCreate GetCreateNote(ActivityPubNote note, Actor actor)
    {
        return new ActivityPubCreate()
        {
            Id = $"{note.Id}/create",
            Actor = actor.Uri?.ToString()!,
            Published = note.Published,
            Object = note
        };
    }
}
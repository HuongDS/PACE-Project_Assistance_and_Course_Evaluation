namespace PACE.API.Controllers;

public record ProposalFile(string FileName, string ContentType, byte[] Content);

using TaskFlow.Api.Services.Assistant.Rules;
using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Dtos.Communication.Mail;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Dtos.Profile;
using TaskFlow.Api.Dtos.Management.EmployeeManagement;
using TaskFlow.Api.Common.Exceptions;
using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using Task = System.Threading.Tasks.Task;
using TaskFlow.Api.Repositories.Assistant.Audits;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Services.Assistant.AuditLog;

public class AssistantAuditLog : IAssistantAuditLog
{
    private readonly IAssistantAuditRepository _audits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public AssistantAuditLog(IAssistantAuditRepository audits, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _audits = audits;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task RecordAsync(int userId, AssistantActionKind kind, string outcome, CancellationToken cancellationToken = default)
    {
        _audits.Add(new AssistantAudit
        {
            UserId = userId,
            Kind = kind,
            Outcome = outcome,
            At = _timeProvider.GetUtcNow().UtcDateTime
        });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

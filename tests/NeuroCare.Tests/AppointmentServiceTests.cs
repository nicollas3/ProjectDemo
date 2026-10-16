using NeuroCare.Application;
using NeuroCare.Domain;

namespace NeuroCare.Tests;

public class AppointmentServiceTests
{
    // Relógio fake: "agora" = 10/03/2026 12:00 local.
    private static SaveAppointmentDto Slot(Scenario s, Guid patientId, int hour, int minute = 0) => new()
    {
        PatientId = patientId, DoctorId = s.DoctorA,
        StartsAtLocal = new DateTime(2026, 3, 11, hour, minute, 0), DurationMinutes = 30
    };

    [Fact]
    public async Task Create_schedules_appointment()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));

        var id = await env.Appointments.CreateAsync(Slot(s, s.PatientA1, 9));

        var list = await env.Appointments.ListAsync(null, null, null);
        Assert.Contains(list, a => a.Id == id && a.Status == AppointmentStatus.Scheduled);
    }

    [Fact]
    public async Task Overlapping_appointment_for_same_doctor_is_rejected()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));

        await env.Appointments.CreateAsync(Slot(s, s.PatientA1, 9));

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            env.Appointments.CreateAsync(Slot(s, s.PatientA2, 9, 15)));
    }

    [Fact]
    public async Task Appointment_in_the_past_is_rejected()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));

        var dto = Slot(s, s.PatientA1, 9);
        dto.StartsAtLocal = new DateTime(2026, 3, 1, 9, 0, 0);

        await Assert.ThrowsAsync<RequestValidationException>(() => env.Appointments.CreateAsync(dto));
    }

    [Fact]
    public async Task Cannot_schedule_patient_from_another_organization()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            env.Appointments.CreateAsync(Slot(s, s.PatientB1, 10)));
    }

    [Fact]
    public async Task Cancelled_slot_can_be_reused()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));

        var first = await env.Appointments.CreateAsync(Slot(s, s.PatientA1, 9));
        await env.Appointments.TransitionAsync(first, AppointmentAction.Cancel);

        var second = await env.Appointments.CreateAsync(Slot(s, s.PatientA2, 9));
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Invalid_transition_is_reported_as_validation_error()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));

        var id = await env.Appointments.CreateAsync(Slot(s, s.PatientA1, 9));
        await env.Appointments.TransitionAsync(id, AppointmentAction.Cancel);

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            env.Appointments.TransitionAsync(id, AppointmentAction.Complete));
    }

    [Fact]
    public async Task Patient_sees_only_own_appointments()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);

        using (var staff = new TestEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA)))
        {
            await staff.Appointments.CreateAsync(Slot(s, s.PatientA1, 9));
            await staff.Appointments.CreateAsync(Slot(s, s.PatientA2, 10));
        }

        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Patient, s.OrgA, s.PatientA1UserId));
        var mine = await env.Appointments.ListAsync(null, null, null);

        Assert.Single(mine);
        Assert.Equal(s.PatientA1, mine[0].PatientId);
    }

    [Fact]
    public async Task Patient_cannot_create_appointments()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Patient, s.OrgA, s.PatientA1UserId));

        await Assert.ThrowsAsync<ForbiddenException>(() => env.Appointments.CreateAsync(Slot(s, s.PatientA1, 9)));
    }
}

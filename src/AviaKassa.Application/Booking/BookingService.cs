namespace AviaKassa.Application.Booking;
/// <summary>Сервис бронирования мест (FR-06).</summary>
public sealed class BookingService : IBookingService
{
    private readonly AviaDbContext _db;
    private readonly IFareCalculator _calculator;

    public BookingService(AviaDbContext db, IFareCalculator calculator)
        => (_db, _calculator) = (db, calculator);

    public async Task<Booking> CreateAsync(CreateBookingRequest req, CancellationToken ct)
    {
        var fare = await _db.FareClasses.FirstAsync(c => c.Id == req.FareClassId, ct);

        int taken = await _db.Bookings
            .Where(b => b.FareClassId == fare.Id)
            .SumAsync(b => b.Passengers.Count, ct);

        if (taken + req.Passengers.Count > fare.SeatsTotal)
            throw new SeatsNotAvailableException(fare.Code, 0);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            FareClassId = fare.Id,
            Status = BookingStatus.Held,
            CreatedUtc = DateTime.Now,
            TimeLimitUtc = DateTime.Now.AddHours(2)
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(ct);
        return booking;
    }
}

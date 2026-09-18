/// A board column. Mirrors the backend's `Status` shape — see
/// `backend/src/Weaver.Domain/Status.cs` — but is defined locally since the
/// board has no API to fetch it from yet (Milestone 5).
class BoardStatus {
  const BoardStatus({
    required this.id,
    required this.name,
    required this.order,
  });

  final String id;
  final String name;
  final int order;
}

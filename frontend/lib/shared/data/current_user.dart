/// Who is signed in, for features that only need the id (e.g. "is this my
/// comment?") without depending on the auth feature.
abstract class CurrentUser {
  String? get currentUserId;
}

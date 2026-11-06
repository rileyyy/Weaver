import 'dart:async';

import 'package:weaver/core/network/api_exception.dart';

/// For a failed record `.wait`: rethrows the first error that isn't an
/// [ApiException], so a programming error isn't reported as a load
/// failure. Returns normally if every failure was an API failure.
void rethrowNonApiErrors(Iterable<AsyncError?> errors) {
  for (final error in errors.nonNulls) {
    if (error.error is! ApiException) {
      Error.throwWithStackTrace(error.error, error.stackTrace);
    }
  }
}

/// The errors of a failed record `.wait`, one per field, for
/// [rethrowNonApiErrors]. Records of two to six fields are supported.
List<AsyncError?> asyncErrorsOf(Object? errors) => switch (errors) {
  (final AsyncError? a, final AsyncError? b) => [a, b],
  (final AsyncError? a, final AsyncError? b, final AsyncError? c) => [a, b, c],
  (
    final AsyncError? a,
    final AsyncError? b,
    final AsyncError? c,
    final AsyncError? d,
  ) =>
    [a, b, c, d],
  (
    final AsyncError? a,
    final AsyncError? b,
    final AsyncError? c,
    final AsyncError? d,
    final AsyncError? e,
  ) =>
    [a, b, c, d, e],
  (
    final AsyncError? a,
    final AsyncError? b,
    final AsyncError? c,
    final AsyncError? d,
    final AsyncError? e,
    final AsyncError? f,
  ) =>
    [a, b, c, d, e, f],
  _ => throw StateError('Unexpected parallel wait errors: $errors'),
};

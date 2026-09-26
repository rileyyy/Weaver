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

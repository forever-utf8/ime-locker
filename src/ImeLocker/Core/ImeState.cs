namespace ImeLocker.Core;

public record ImeState(nint KeyboardLayout, uint ConversionMode, uint SentenceMode);

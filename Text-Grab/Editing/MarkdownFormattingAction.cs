using System;

namespace Text_Grab.Editing;

[Flags]
internal enum MarkdownFormattingAction
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Heading = 4,
    Bullets = 8,
    Numbering = 16,
    Quote = 32,
    CodeBlock = 64,
    Strikethrough = 128,
    Superscript = 256,
    Subscript = 512,
    ToDo = 1024
}

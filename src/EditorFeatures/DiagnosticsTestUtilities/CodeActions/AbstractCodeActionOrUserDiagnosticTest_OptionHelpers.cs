﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.Editor.UnitTests.CodeActions
{
    public abstract partial class AbstractCodeActionOrUserDiagnosticTest
    {
        internal (OptionKey2, object?) SingleOption<T>(Option2<T> option, T enabled)
            => (new OptionKey2(option), enabled);

        internal (OptionKey2, object?) SingleOption<T>(PerLanguageOption2<T> option, T value)
            => (new OptionKey2(option, this.GetLanguage()), value);

        internal (OptionKey2, object) SingleOption<T>(Option2<CodeStyleOption2<T>> option, T enabled, NotificationOption2 notification)
            => (new OptionKey2(option), new CodeStyleOption2<T>(enabled, notification));

        internal (OptionKey2, object) SingleOption<T>(Option2<CodeStyleOption2<T>> option, CodeStyleOption2<T> codeStyle)
            => (new OptionKey2(option), codeStyle);

        internal (OptionKey2, object) SingleOption<T>(PerLanguageOption2<CodeStyleOption2<T>> option, T enabled, NotificationOption2 notification)
            => (new OptionKey2(option, this.GetLanguage()), new CodeStyleOption2<T>(enabled, notification));

        internal (OptionKey2, object) SingleOption<T>(PerLanguageOption2<CodeStyleOption2<T>> option, CodeStyleOption2<T> codeStyle)
            => (new OptionKey2(option, this.GetLanguage()), codeStyle);

        internal static (OptionKey2, object) SingleOption<T>(PerLanguageOption2<CodeStyleOption2<T>> option, CodeStyleOption2<T> codeStyle, string language)
            => (new OptionKey2(option, language), codeStyle);

        internal OptionsCollection Option<T>(Option2<CodeStyleOption2<T>> option, T enabled, NotificationOption2 notification)
            => OptionsSet(SingleOption(option, enabled, notification));

        internal OptionsCollection Option<T>(Option2<CodeStyleOption2<T>> option, CodeStyleOption2<T> codeStyle)
            => OptionsSet(SingleOption(option, codeStyle));

        internal OptionsCollection Option<T>(PerLanguageOption2<CodeStyleOption2<T>> option, T enabled, NotificationOption2 notification)
            => OptionsSet(SingleOption(option, enabled, notification));

        internal OptionsCollection Option<T>(Option2<T> option, T value)
            => OptionsSet(SingleOption(option, value));

        internal OptionsCollection Option<T>(PerLanguageOption2<T> option, T value)
            => OptionsSet(SingleOption(option, value));

        internal OptionsCollection Option<T>(PerLanguageOption2<CodeStyleOption2<T>> option, CodeStyleOption2<T> codeStyle)
            => OptionsSet(SingleOption(option, codeStyle));

        internal OptionsCollection OptionsSet(OptionKey2 option, object value)
            => OptionsSet((option, value));

        internal OptionsCollection OptionsSet(params (OptionKey2 key, object? value)[] options)
            => new OptionsCollection(GetLanguage(), options: options);
    }
}

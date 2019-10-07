using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Elastic.Apm.Helpers
{
	public abstract class WildcardMatcher
	{
//		public static final String DOCUMENTATION = "This option supports the wildcard `*`, which matches zero or more characters.\n" +
//		"Examples: `/foo/*/bar/*/baz*`, `*foo*`.\n" +
//		"Matching is case insensitive by default.\n" +
//		"Prepending an element with `(?-i)` makes the matching case sensitive.";
//
		private static string CASE_INSENSITIVE_PREFIX = "(?i)";

//
		private static string CASE_SENSITIVE_PREFIX = "(?-i)";

//
		private static string WILDCARD = "*";
//
//		private static final WildcardMatcher MATCH_ALL = valueOf(WILDCARD);
//
//		public static WildcardMatcher caseSensitiveMatcher(String matcher)
//		{
//			return valueOf(CASE_SENSITIVE_PREFIX + matcher);
//		}
//
//		public static WildcardMatcher matchAll()
//		{
//			return MATCH_ALL;
//		}

		/**
		 * Constructs a new {@link WildcardMatcher} via a wildcard string.
		 * <p>
		 * It supports the {@code *} wildcard which matches zero or more characters.
		 * </p>
		 * <p>
		 * By default, matches are a case insensitive.
		 * Prepend {@code (?-i)} to your pattern to make it case sensitive.
		 * Example: {@code (?-i)foo*} matches the string {@code foobar} but does not match {@code FOOBAR}.
		 * </p>
		 * <p>
		 * It does NOT support single character wildcards like {@code f?o}
		 * </p>
		 *
		 * @param wildcardString The wildcard string.
		 * @return The {@link WildcardMatcher}
		 */
		public static WildcardMatcher ValueOf(string wildcardString)
		{
			var matcher = wildcardString;
			var ignoreCase = true;
			if (matcher.StartsWith(CASE_SENSITIVE_PREFIX))
			{
				ignoreCase = false;
				matcher = matcher.Substring(CASE_SENSITIVE_PREFIX.Length);
			}
			else if (matcher.StartsWith(CASE_INSENSITIVE_PREFIX))
			{
				matcher = matcher.Substring(CASE_INSENSITIVE_PREFIX.Length);
			}

			var split = matcher.Split('*');
			if (split.Length == 1)
			{
				return new SimpleWildcardMatcher(split[0], matcher.StartsWith(WILDCARD), matcher.EndsWith(WILDCARD), ignoreCase);
			}

			var matchers = new List<SimpleWildcardMatcher>(split.Length);
			for (var i = 0; i < split.Length; i++)
			{
				bool isFirst = i == 0;
				bool isLast = i == split.Length - 1;
				matchers.Add(new SimpleWildcardMatcher(split[i],
					!isFirst || matcher.StartsWith(WILDCARD),
					!isLast || matcher.EndsWith(WILDCARD),
					ignoreCase));
			}
			return new CompoundWildcardMatcher(wildcardString, matcher, matchers);
		}

		/**
		 * Returns the first {@link WildcardMatcher} {@linkplain WildcardMatcher#matches(String) matching} the provided string.
		 *
		 * @param matchers the matchers which should be used to match the provided string
		 * @param s        the string to match against
		 * @return the first matching {@link WildcardMatcher}, or {@code null} if none match.
		 */
		public static bool IsAnyMatch(List<WildcardMatcher> matchers, string s)
		{
			return AnyMatch(matchers, s) != null;
		}

		/**
		 * Returns {@code true}, if any of the matchers match the provided string.
		 *
		 * @param matchers the matchers which should be used to match the provided string
		 * @param s        the string to match against
		 * @return {@code true}, if any of the matchers match the provided string
		 */

		public static WildcardMatcher AnyMatch(List<WildcardMatcher> matchers, string s)
		{
			if (s == null)
			{
				return null;
			}
			return AnyMatch(matchers, s, null);
		}

		/**
		 * Returns the first {@link WildcardMatcher} {@linkplain WildcardMatcher#matches(String) matching} the provided partitioned string.
		 *
		 * @param matchers   the matchers which should be used to match the provided string
		 * @param firstPart  The first part of the string to match against.
		 * @param secondPart The second part of the string to match against.
		 * @return the first matching {@link WildcardMatcher}, or {@code null} if none match.
		 * @see #matches(String, String)
		 */
		public static WildcardMatcher AnyMatch(List<WildcardMatcher> matchers, string firstPart, string secondPart)
		{
			for (int i = 0; i < matchers.Count; i++)
			{
				if (matchers.ElementAt(i).Matches(firstPart, secondPart))
				{
					return matchers.ElementAt(i);
				}
			}
			return null;
		}

		/*
		 * Based on https://stackoverflow.com/a/29809553/1125055
		 * Thx to Zach Vorhies
		 */
		public static int indexOfIgnoreCase(string haystack1, string haystack2, string needle, bool ignoreCase,  int start, int end)
		{
			if (start < 0)
			{
				return -1;
			}
			int totalHaystackLength = haystack1.Length + haystack2.Length;
			if (needle.IsEmpty() || totalHaystackLength == 0)
			{
				// Fallback to legacy behavior.
				return haystack1.IndexOf(needle);
			}

			var haystack1Length = haystack1.Length;
			var needleLength = needle.Length;
			for (var i = start; i < end; i++)
			{
				// Early out, if possible.
				if (i + needleLength > totalHaystackLength)
				{
					return -1;
				}

				// Attempt to match substring starting at position i of haystack.
				int j = 0;
				int ii = i;
				while (ii < totalHaystackLength && j < needleLength)
				{
					char c = ignoreCase
						? char.ToLowerInvariant(CharAt(ii, haystack1, haystack2, haystack1Length))
						: CharAt(ii, haystack1, haystack2, haystack1Length);
					char c2 = ignoreCase ? char.ToLowerInvariant(needle.ElementAt(j)) : needle.ElementAt(j);
					if (c != c2)
					{
						break;
					}
					j++;
					ii++;
				}
				// Walked all the way to the end of the needle, return the start
				// position that this was found.
				if (j == needleLength)
				{
					return i;
				}
			}

			return -1;
		}

		private static char CharAt(int i, string firstPart, string secondPart, int firstPartLength)
		{
			return i < firstPartLength ? firstPart.ElementAt(i) : secondPart.ElementAt(i - firstPartLength);
		}


		/**
		 * Checks if the given string matches the wildcard pattern.
		 *
		 * @param s the String to match
		 * @return whether the String matches the given pattern
		 */
		public abstract bool Matches(string s);

		/**
		 * This is a different version of {@link #matches(String)} which has the same semantics as calling
		 * {@code matcher.matches(firstPart + secondPart);}.
		 * <p>
		 * The difference is that this method does not allocate memory.
		 * </p>
		 *
		 * @param firstPart  The first part of the string to match against.
		 * @param secondPart The second part of the string to match against.
		 * @return {@code true},
		 * when the wildcard pattern matches the partitioned string,
		 * {@code false} otherwise.
		 */
		public abstract bool Matches(string firstPart, string secondPart);

//		@Override
//
//		public boolean equals(Object obj)
//		{
//			if (!(obj instanceof WildcardMatcher)) {
//				return false;
//			}
//			return toString().equals(obj.toString());
//		}

		public abstract string GetMatcher();

		/**
		 * This {@link WildcardMatcher} supports wildcards in the middle of the matcher by decomposing the matcher into several
		 * {@link SimpleWildcardMatcher}s.
		 */
		class CompoundWildcardMatcher : WildcardMatcher
		{
			private string wildcardString;
			private string matcher;
			private List<SimpleWildcardMatcher> wildcardMatchers;

			public CompoundWildcardMatcher(String wildcardString, String matcher, List<SimpleWildcardMatcher> wildcardMatchers)
			{
				this.wildcardString = wildcardString;
				this.matcher = matcher;
				this.wildcardMatchers = wildcardMatchers;
			}

			public override bool Matches(string s)
			{
				int offset = 0;
				for (int i = 0; i < wildcardMatchers.Count(); i++)
				{
					SimpleWildcardMatcher matcher = wildcardMatchers.ElementAt(i);
					offset = matcher.IndexOf(s, offset);
					if (offset == -1)
					{
						return false;
					}
					offset += matcher.matcher.Length;
				}
				return true;
			}

			public override bool Matches(string firstPart, string secondPart)
			{
				int offset = 0;
				for (int i = 0; i < wildcardMatchers.Count; i++)
				{
					var matcher = wildcardMatchers.ElementAt(i);
					offset = matcher.IndexOf(firstPart, secondPart, offset);
					if (offset == -1)
					{
						return false;
					}
					offset += matcher.matcher.Length;
				}
				return true;
			}

//			@Override
//
//			public String toString()
//			{
//				return wildcardString;
//			}



			public override string GetMatcher()
			{
				return matcher;
			}
		}

		/**
		 * This {@link} does not support wildcards in the middle of a matcher.
		 */
		class SimpleWildcardMatcher : WildcardMatcher
		{
			public string matcher;

			private string stringRepresentation;

			private bool wildcardAtEnd;

			private bool wildcardAtBeginning;

			private bool ignoreCase;

			public SimpleWildcardMatcher(string matcher, bool wildcardAtBeginning, bool wildcardAtEnd, bool ignoreCase)
			{
				this.matcher = matcher;
				this.wildcardAtEnd = wildcardAtEnd;
				this.wildcardAtBeginning = wildcardAtBeginning;
				this.ignoreCase = ignoreCase;
				this.stringRepresentation =
					new StringBuilder(matcher.Length + CASE_SENSITIVE_PREFIX.Length + WILDCARD.Length + WILDCARD.Length)
						.Append(ignoreCase ? "" : CASE_SENSITIVE_PREFIX)
						.Append(wildcardAtBeginning ? WILDCARD : "")
						.Append(matcher)
						.Append(wildcardAtEnd ? WILDCARD : "")
						.ToString();
			}

//			public String toString()
//			{
//				return stringRepresentation;
//			}

			public override bool Matches(string s)
			{
				return IndexOf(s, 0) != -1;
			}

			public override bool Matches(string firstPart, string secondPart)
			{
				return IndexOf(firstPart, secondPart, 0) != -1;
			}

			public int IndexOf(string s, int offset) {
				return IndexOf(s, "", offset);
			}

			public int IndexOf(string firstPart, string secondPart, int offset) {
				if (secondPart == null)
				{
					secondPart = "";
				}
				int totalLength = firstPart.Length + secondPart.Length;
				if (wildcardAtEnd && wildcardAtBeginning)
				{
					return indexOfIgnoreCase(firstPart, secondPart, matcher, ignoreCase, offset, totalLength);
				}
				else if (wildcardAtEnd)
				{
					return indexOfIgnoreCase(firstPart, secondPart, matcher, ignoreCase, 0, 1);
				}
				else if (wildcardAtBeginning)
				{
					return indexOfIgnoreCase(firstPart, secondPart, matcher, ignoreCase, totalLength - matcher.Length, totalLength);
				}
				else if (totalLength == matcher.Length)
				{
					return indexOfIgnoreCase(firstPart, secondPart, matcher, ignoreCase, 0, totalLength);
				}
				else
				{
					return -1;
				}
			}

			public override string GetMatcher()
			{
				return matcher;
			}
		}
	}

}

// format a container name
// Azure eventhub names are 6-50 characters long; lower-case letters, numbers, and hyphens; must start and end with a letter or number
@description('Format an event name following the Azure Eventhub naming conventions')
param eventhubName string
var lowercasedName = toLower(eventhubName)
var replaceUndercores = replace(lowercasedName, '_', '-')
var replaceDashes2 = replace(replaceUndercores, '--', '-')
var replaceDashes3 = replace(replaceDashes2, '--', '-')
var replaceDashes4 = replace(replaceDashes3, '--', '-')
var removeInvalidChars1 = replace(
  replace(
    replace(
      replace(
        replace(
          replace(
            replace(
              replace(
                replace(
                  replace(
                    replaceDashes4,
                    '!', ''
                  ),
                  '@', ''
                ),
                '#', ''
              ),
              '$', ''
            ),
            '%', ''
          ),
          '^', ''
        ),
        '&', ''
      ),
      '*', ''
    ),
    '(', ''
  ),
  ')', ''
)

var removeInvalidChars2 = replace(
  replace(
    replace(
      replace(
        replace(
          replace(
            replace(
              replace(
                replace(
                  replace(
                    removeInvalidChars1,
                    '+', ''
                  ),
                  '=', ''
                ),
                '/', ''
              ),
              '\\', ''
            ),
            ',', ''
          ),
          '.', ''
        ),
        '"', ''
      ),
      '\'', ''
    ),
    '[', ''
  ),
  ']', ''
)

var removeInvalidChars3 = replace(
  replace(
    replace(
      replace(
        replace(
          replace(
            replace(
              removeInvalidChars2,
              '{', ''
            ),
            '}', ''
          ),
          '|', ''
        ),
        '`', ''
      ),
      '?', ''
    ),
    '>', ''
  ),
  '<', ''
)

var singeleDashFinal = replace(removeInvalidChars3, '--', '-')

var trimStart = startsWith(singeleDashFinal, '-') ? substring(singeleDashFinal, 1, length(singeleDashFinal) - 1) : singeleDashFinal
var trimEnd = endsWith(trimStart, '-') ? substring(removeInvalidChars3, length(trimStart) - 1) : trimStart
var minSizeName = length(trimEnd) < 6 ? '${trimEnd}-eventhub' : trimEnd

output formattedEventHubName string = take(minSizeName, 50)

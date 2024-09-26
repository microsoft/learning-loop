// format a container name
// The container name must contain no more than 63 characters and must match the regex '[a-z0-9]([-a-z0-9]*[a-z0-9])?' (e.g. 'my-name')
@description('Format a container name following the Azure Container Instance naming conventions')
param containerName string
var lowercasedContainerName = toLower(containerName)
var replaceUndercores = replace(lowercasedContainerName, '_', '-')
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

output formattedContainerName string = take(trimEnd, 63)

<?xml version="1.0" encoding="utf-8"?>
<!--
  The one report stylesheet for both batch tasks. Studio's report viewer takes the first .xsl
  embedded in the task assembly, so this branches on /task/@name. It carries no text of its
  own: every label comes from the XML's <labels> element, written by the task in the culture it
  ran under, and the CSS and logo come from Studio through the XmlReporting extension object
  its viewer supplies, so the report looks like Studio's own.
-->
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:XmlReporting="urn:XmlReporting"
                exclude-result-prefixes="XmlReporting">
  <xsl:output method="html" indent="yes" />

  <xsl:key name="label" match="/task/labels/label" use="@id" />

  <xsl:template match="/task">
    <html>
      <head>
        <xsl:value-of select="XmlReporting:GetDefaultCssLinkTag()" disable-output-escaping="yes" />
      </head>
      <body>
        <table width="100%" border="0" cellpadding="0" cellspacing="0">
          <tr>
            <td width="100%">
              <h1><xsl:value-of select="key('label', 'title')" /></h1>
            </td>
            <td valign="top">
              <img>
                <xsl:attribute name="src"><xsl:value-of select="XmlReporting:GetImagesUrl()" />/TradosStudio_Logo.svg</xsl:attribute>
              </img>
            </td>
          </tr>
        </table>

        <h2 class="first"><xsl:value-of select="key('label', 'summary')" /></h2>
        <table class="InfoList" width="100%" border="0" cellpadding="0" cellspacing="2">
          <tr>
            <td class="InfoItem"><xsl:value-of select="key('label', 'project')" /></td>
            <td class="InfoData"><xsl:value-of select="taskInfo/@project" /></td>
          </tr>
          <tr>
            <td class="InfoItem"><xsl:value-of select="key('label', 'languages')" /></td>
            <td class="InfoData">
              <xsl:value-of select="taskInfo/@sourceLanguage" />
              <xsl:text> - </xsl:text>
              <xsl:value-of select="taskInfo/@targetLanguage" />
            </td>
          </tr>
          <tr>
            <td class="InfoItem"><xsl:value-of select="key('label', 'files')" /></td>
            <td class="InfoData"><xsl:value-of select="taskInfo/@files" /></td>
          </tr>
          <tr>
            <td class="InfoItem"><xsl:value-of select="key('label', 'createdAt')" /></td>
            <td class="InfoData"><xsl:value-of select="taskInfo/@runAt" /></td>
          </tr>
          <tr>
            <td class="InfoItem"><xsl:value-of select="key('label', 'cldr')" /></td>
            <td class="InfoData"><xsl:value-of select="taskInfo/@cldr" /></td>
          </tr>
        </table>

        <h2><xsl:value-of select="key('label', 'settings')" /></h2>
        <table class="InfoList" width="100%" border="0" cellpadding="0" cellspacing="2">
          <xsl:for-each select="settings/setting">
            <tr>
              <td class="InfoItem"><xsl:value-of select="@label" /></td>
              <td class="InfoData"><xsl:value-of select="@value" /></td>
            </tr>
          </xsl:for-each>
        </table>

        <xsl:choose>
          <xsl:when test="@name = 'expand'">
            <xsl:call-template name="ExpandBody" />
          </xsl:when>
          <xsl:otherwise>
            <xsl:call-template name="FinaliseBody" />
          </xsl:otherwise>
        </xsl:choose>
      </body>
    </html>
  </xsl:template>

  <!-- ==================== expand ==================== -->

  <xsl:template name="ExpandBody">
    <h2><xsl:value-of select="key('label', 'totals')" /></h2>
    <table class="ReportTable" border="0" cellspacing="0" cellpadding="2" width="100%">
      <xsl:call-template name="ExpandHeading" />
      <tbody>
        <xsl:for-each select="file">
          <tr>
            <td class="File" align="left"><xsl:value-of select="@name" /></td>
            <xsl:call-template name="ExpandCounts" />
          </tr>
        </xsl:for-each>
        <tr>
          <td class="File" align="left"><xsl:value-of select="key('label', 'total')" /></td>
          <xsl:for-each select="totals">
            <xsl:call-template name="ExpandCounts">
              <xsl:with-param name="class" select="'Total'" />
            </xsl:call-template>
          </xsl:for-each>
        </tr>
      </tbody>
    </table>

    <h2><xsl:value-of select="key('label', 'details')" /></h2>
    <xsl:for-each select="file">
      <h3><xsl:value-of select="@name" /></h3>
      <table class="ReportTable" border="0" cellspacing="0" cellpadding="2" width="100%">
        <thead>
          <tr>
            <th class="TypeHead" align="left"><xsl:value-of select="key('label', 'key')" /></th>
            <th class="TypeHead" align="left"><xsl:value-of select="key('label', 'outcome')" /></th>
            <th class="Unit"><xsl:value-of select="key('label', 'segments')" /></th>
            <th class="TypeHead" align="left" width="50%"><xsl:value-of select="key('label', 'note')" /></th>
          </tr>
        </thead>
        <tbody>
          <xsl:for-each select="message">
            <tr>
              <td class="File" align="left"><xsl:value-of select="@key" /></td>
              <td align="left"><xsl:value-of select="key('label', concat('outcome_', @outcome))" /></td>
              <td class="Unit"><xsl:value-of select="@segments" /></td>
              <td align="left"><xsl:value-of select="@detail" /></td>
            </tr>
          </xsl:for-each>
        </tbody>
      </table>
    </xsl:for-each>
  </xsl:template>

  <xsl:template name="ExpandHeading">
    <thead>
      <tr>
        <th class="TypeHead" align="left" width="100%"><xsl:value-of select="key('label', 'file')" /></th>
        <th class="Unit"><xsl:value-of select="key('label', 'messages')" /></th>
        <th class="Unit"><xsl:value-of select="key('label', 'outcome_Expanded')" /></th>
        <th class="Unit"><xsl:value-of select="key('label', 'outcome_Protected')" /></th>
        <th class="Unit"><xsl:value-of select="key('label', 'outcome_Walked')" /></th>
        <th class="Unit"><xsl:value-of select="key('label', 'outcome_PassedThrough')" /></th>
        <th class="Unit"><xsl:value-of select="key('label', 'outcome_Skipped')" /></th>
        <th class="Unit"><xsl:value-of select="key('label', 'segments')" /></th>
        <th class="Unit"><xsl:value-of select="key('label', 'warnings')" /></th>
      </tr>
    </thead>
  </xsl:template>

  <xsl:template name="ExpandCounts">
    <xsl:param name="class" select="'Unit'" />
    <td class="{$class}"><xsl:value-of select="@units" /></td>
    <td class="{$class}"><xsl:value-of select="@expanded" /></td>
    <td class="{$class}"><xsl:value-of select="@protected" /></td>
    <td class="{$class}"><xsl:value-of select="@walked" /></td>
    <td class="{$class}"><xsl:value-of select="@passedThrough" /></td>
    <td class="{$class}"><xsl:value-of select="@skipped" /></td>
    <td class="{$class}"><xsl:value-of select="@segments" /></td>
    <td class="{$class}"><xsl:value-of select="@warnings" /></td>
  </xsl:template>

  <!-- ==================== finalise ==================== -->

  <xsl:template name="FinaliseBody">
    <h2><xsl:value-of select="key('label', 'totals')" /></h2>
    <table class="ReportTable" border="0" cellspacing="0" cellpadding="2" width="100%">
      <thead>
        <tr>
          <th class="TypeHead" align="left" width="100%"><xsl:value-of select="key('label', 'file')" /></th>
          <th class="Unit"><xsl:value-of select="key('label', 'messages')" /></th>
          <th class="Unit"><xsl:value-of select="key('label', 'segments')" /></th>
          <th class="Unit"><xsl:value-of select="key('label', 'pruned')" /></th>
          <th class="Unit"><xsl:value-of select="key('label', 'filled')" /></th>
          <th class="Unit"><xsl:value-of select="key('label', 'warnings')" /></th>
        </tr>
      </thead>
      <tbody>
        <xsl:for-each select="file">
          <tr>
            <td class="File" align="left"><xsl:value-of select="@name" /></td>
            <xsl:call-template name="FinaliseCounts" />
          </tr>
        </xsl:for-each>
        <tr>
          <td class="File" align="left"><xsl:value-of select="key('label', 'total')" /></td>
          <xsl:for-each select="totals">
            <xsl:call-template name="FinaliseCounts">
              <xsl:with-param name="class" select="'Total'" />
            </xsl:call-template>
          </xsl:for-each>
        </tr>
      </tbody>
    </table>

    <h2><xsl:value-of select="key('label', 'warnings')" /></h2>
    <xsl:choose>
      <xsl:when test="file/message/warning">
        <table class="ReportTable" border="0" cellspacing="0" cellpadding="2" width="100%">
          <thead>
            <tr>
              <th class="TypeHead" align="left"><xsl:value-of select="key('label', 'file')" /></th>
              <th class="TypeHead" align="left"><xsl:value-of select="key('label', 'key')" /></th>
              <th class="TypeHead" align="left" width="60%"><xsl:value-of select="key('label', 'warnings')" /></th>
            </tr>
          </thead>
          <tbody>
            <xsl:for-each select="file/message/warning">
              <tr>
                <td class="File" align="left"><xsl:value-of select="../../@name" /></td>
                <td align="left"><xsl:value-of select="../@key" /></td>
                <td align="left"><xsl:value-of select="." /></td>
              </tr>
            </xsl:for-each>
          </tbody>
        </table>
      </xsl:when>
      <xsl:otherwise>
        <p><xsl:value-of select="key('label', 'noWarnings')" /></p>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <xsl:template name="FinaliseCounts">
    <xsl:param name="class" select="'Unit'" />
    <td class="{$class}"><xsl:value-of select="@units" /></td>
    <td class="{$class}"><xsl:value-of select="@segments" /></td>
    <td class="{$class}"><xsl:value-of select="@pruned" /></td>
    <td class="{$class}"><xsl:value-of select="@filled" /></td>
    <td class="{$class}"><xsl:value-of select="@warnings" /></td>
  </xsl:template>
</xsl:stylesheet>

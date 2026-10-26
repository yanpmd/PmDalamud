UDT Specification
=================

> _Work in progress._

Una.Drawing Template (UDT) is an XML-based template language designed for creating
(reusable) layout components.

An UDT document consists of one root element (`<udt>`), which contains one or more
child elements. The root element cannot be empty.

```xml
<udt>
    <!-- CDATA blocks are used to define the stylesheet -->
    <!-- There can be one CDATA block in the UDT element -->
    <![CDATA[
    #main {
        background-color: 0xFF123456;
    }
    ]]>
    
    <!-- Templates are used to define reusable components -->
    <template name="my-custom-element">
        <argument name="label" type="string" default="My Button"/>
        <argument name="icon" type="int32"/>
        <root>
            <node class="button">
                <node class="icon" style="icon-id: ${icon}"/>
                <node class="body">
                    <slot/>
                </node>
            </node>
        </root>
    </template>
    
    <!-- Next to CDATA and template element, only one root element is allowed -->
    <node>
        <my-custom-element icon="113">
            This is the label.
        </my-custom-element>
    </node>
</udt>
```

## Importing templates from other UDT files

Defined templates can be referenced from other UDT files. This is done by using
the `import` element in the UDT element. Note that this only works if the
current UDT file is loaded from an assembly and that the referenced UDT file
is also part of the assembly.

```xml
<udt>
    <import from="my_other_udt.xml"/>
    
    <my-custom-element icon="113">
        This is the label.
    </my-custom-element>
</udt>
```

```csharp
UdtDocument doc = UdtLoader.LoadFromAssembly(
    Assembly.GetExecutingAssembly(),
    "my_udt.xml"
);
```

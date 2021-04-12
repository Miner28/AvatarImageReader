from PIL import Image;


def set_bit(value, state, bit):
    if (state):
        return value | (1 << bit)
    else:
        return value & ~(1 << bit)


def read_bit(value, index):
    return (value >> index) & 1


input = "This is a test string for encoding! #&đ~ˇ^˛°˛``˙˙`]"

byte_list = input.encode('utf-32')

print("----------------")

image_width = 1200
image_height = 900
print(f"Max size: {image_height*image_height/9-4}")
print(f"Used size: {len(input)}")
img = Image.new("RGB", [image_width, image_height], (255, 255, 255))

index = 0

for d in byte_list:
    bit0 = read_bit(d, 0)
    bit1 = read_bit(d, 1)
    bit2 = read_bit(d, 2)
    bit3 = read_bit(d, 3)
    bit4 = read_bit(d, 4)
    bit5 = read_bit(d, 5)
    bit6 = read_bit(d, 6)
    bit7 = read_bit(d, 7)

    rValue1 = 0
    if bit0:
        rValue1 = 255

    gValue1 = 0
    if bit1:
        gValue1 = 255

    bValue1 = 0
    if bit2:
        bValue1 = 255

    rValue2 = 0
    if bit3:
        rValue2 = 255

    gValue2 = 0
    if bit4:
        gValue2 = 255

    bValue2 = 0
    if bit5:
        bValue2 = 255

    rValue3 = 0
    if bit6:
        rValue3 = 255

    gValue3 = 0
    if bit7:
        gValue3 = 255

    img.putpixel((((img.width - 1) - index) % img.width, index // img.width), (rValue1, gValue1, bValue1))
    index += 1
    img.putpixel((((img.width - 1) - index) % img.width, index // img.width), (rValue2, gValue2, bValue2))
    index += 1
    img.putpixel((((img.width - 1) - index) % img.width, index // img.width), (rValue3, gValue3, 0))
    index += 1

img.save("img.png")


for b in byte_list:
    #print(b)
    pass
print("----------------")
